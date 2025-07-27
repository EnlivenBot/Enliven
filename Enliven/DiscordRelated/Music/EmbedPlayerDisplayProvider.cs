using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Interactions.Handlers;
using Common;
using Common.Config;
using Common.Music.Cluster;
using Discord;
using Lavalink4NET.Artwork;
using Lavalink4NET.Players;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bot.DiscordRelated.Music;

public sealed class EmbedPlayerDisplayProvider : IService, IDisposable {
    private readonly IArtworkService _artworkService;
    private readonly ConcurrentDictionary<string, EmbedPlayerDisplay> _cache = new();
    private readonly EnlivenShardedClient _client;
    private readonly IEnlivenClusterAudioService _clusterAudioService;
    private readonly CommandHandlerService _commandHandlerService;
    private readonly IGuildConfigProvider _guildConfigProvider;
    private readonly ILogger<EmbedPlayerDisplayProvider> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MessageComponentInteractionsHandler _messageComponentInteractionsHandler;
    private readonly CancellationTokenSource _updateCycleCancellationTokenSource = new();

    public EmbedPlayerDisplayProvider(EnlivenShardedClient client, IGuildConfigProvider guildConfigProvider,
        CommandHandlerService commandHandlerService,
        MessageComponentInteractionsHandler messageComponentInteractionsHandler,
        ILogger<EmbedPlayerDisplayProvider> logger, ILoggerFactory loggerFactory, IArtworkService artworkService,
        IEnlivenClusterAudioService clusterAudioService) {
        _messageComponentInteractionsHandler = messageComponentInteractionsHandler;
        _commandHandlerService = commandHandlerService;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _artworkService = artworkService;
        _clusterAudioService = clusterAudioService;
        _client = client;
        _guildConfigProvider = guildConfigProvider;
    }

    public Task OnPreDiscordStart() {
        new Task(UpdateCycle, TaskCreationOptions.LongRunning).Start();
        return Task.CompletedTask;
    }

    public EmbedPlayerDisplay? Get(string id) {
        return _cache.TryGetValue(id, out var display) ? display : null;
    }

    public EmbedPlayerDisplay? Get(ITextChannel channel) {
        return Get($"guild-{channel.GuildId}");
    }

    public EmbedPlayerDisplay Provide(ITextChannel channel) {
        return ProvideInternal($"guild-{channel.GuildId}", channel);
    }

    private EmbedPlayerDisplay ProvideInternal(string id, ITextChannel channel, int recursiveCount = 0) {
        var embedPlayerDisplay = _cache.GetOrAdd(id, s => {
            var guildConfig = _guildConfigProvider.Get(channel.GuildId);
            // TODO: Implement proper logger creation
            return new EmbedPlayerDisplay(channel, _client, guildConfig.Loc, _commandHandlerService,
                _messageComponentInteractionsHandler, _loggerFactory.CreateLogger<EmbedPlayerDisplay>(),
                _artworkService, _clusterAudioService);
        });
        if (!embedPlayerDisplay.IsShutdowned && embedPlayerDisplay.Player?.State != PlayerState.Destroyed)
            return embedPlayerDisplay;
        _cache.TryRemove(id, out _);
        if (recursiveCount <= 1) return ProvideInternal(id, channel, ++recursiveCount);
        _logger.LogCritical("Provider recursive call. Provider: {Data}",
            JsonConvert.SerializeObject(embedPlayerDisplay, Formatting.None,
                new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
        return embedPlayerDisplay;
    }

    private async void UpdateCycle() {
        try {
            while (!_updateCycleCancellationTokenSource.IsCancellationRequested) {
                var waitCycle = Task.Delay(Constants.PlayerEmbedUpdateDelay, _updateCycleCancellationTokenSource.Token);
                var displays = _cache.Values.ToList();
                foreach (var display in displays) {
                    try {
                        if (display.Player?.State != PlayerState.Playing) continue;
                        display.UpdateProgress();
                        display.UpdateMessageComponents();
                        await display.UpdateControlMessage(true);
                    }
                    catch (Exception) {
                        // ignored
                    }
                }

                await waitCycle;
            }
        }
        catch (Exception) {
            // ignored
        }
    }

    public void Dispose() {
        _updateCycleCancellationTokenSource.Cancel();
    }
}