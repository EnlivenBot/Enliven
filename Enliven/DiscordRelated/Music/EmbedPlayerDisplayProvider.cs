using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.MessageComponents;
using Common;
using Common.Config;
using Common.Music.Cluster;
using Discord;
using Lavalink4NET.Artwork;
using Lavalink4NET.Players;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bot.DiscordRelated.Music;

public class EmbedPlayerDisplayProvider : IService, IDisposable
{
    private readonly IArtworkService _artworkService;
    private readonly ConcurrentDictionary<string, EmbedPlayerDisplay> _cache = new();
    private readonly EnlivenShardedClient _client;
    private readonly IEnlivenClusterAudioService _clusterAudioService;
    private readonly CommandHandlerService _commandHandlerService;
    private readonly IGuildConfigProvider _guildConfigProvider;
    private readonly ILogger<EmbedPlayerDisplayProvider> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MessageComponentService _messageComponentService;
    private IDisposable? _restoreStoppedHandled;

    public EmbedPlayerDisplayProvider(EnlivenShardedClient client, IGuildConfigProvider guildConfigProvider,
        CommandHandlerService commandHandlerService, MessageComponentService messageComponentService,
        ILogger<EmbedPlayerDisplayProvider> logger, ILoggerFactory loggerFactory, IArtworkService artworkService,
        IEnlivenClusterAudioService clusterAudioService)
    {
        _messageComponentService = messageComponentService;
        _commandHandlerService = commandHandlerService;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _artworkService = artworkService;
        _clusterAudioService = clusterAudioService;
        _client = client;
        _guildConfigProvider = guildConfigProvider;
    }

    public void Dispose()
    {
        _restoreStoppedHandled?.Dispose();
    }

    public Task OnPreDiscordStart()
    {
        _restoreStoppedHandled = _messageComponentService.MessageComponentUse
            .Where(component => component.Data.CustomId == "restoreStoppedPlayer")
            .SubscribeAsync(component =>
            {
                var guild = (component.Channel as IGuildChannel)?.Guild;
                if (guild == null) return Task.CompletedTask;
                var context = new ControllableCommandContext(_client)
                    { Guild = guild, Channel = component.Channel, User = component.User };
                return _commandHandlerService.ExecuteCommand("resume", context, component.User.Id.ToString());
            });
        new Task(UpdateCycle, TaskCreationOptions.LongRunning).Start();
        return Task.CompletedTask;
    }

    public EmbedPlayerDisplay? Get(string id)
    {
        return _cache.TryGetValue(id, out var display) ? display : null;
    }

    public EmbedPlayerDisplay? Get(ITextChannel channel)
    {
        return Get($"guild-{channel.GuildId}");
    }

    public EmbedPlayerDisplay Provide(ITextChannel channel)
    {
        return ProvideInternal($"guild-{channel.GuildId}", channel);
    }

    private EmbedPlayerDisplay ProvideInternal(string id, ITextChannel channel, int recursiveCount = 0)
    {
        var embedPlayerDisplay = _cache.GetOrAdd(id, s =>
        {
            var guildConfig = _guildConfigProvider.Get(channel.GuildId);
            // TODO: Implement proper logger creation
            return new EmbedPlayerDisplay(channel, _client, guildConfig.Loc, _commandHandlerService,
                _messageComponentService, _loggerFactory.CreateLogger<EmbedPlayerDisplay>(), _artworkService, _clusterAudioService);
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

    private async void UpdateCycle()
    {
        while (true)
        {
            var waitCycle = Task.Delay(Constants.PlayerEmbedUpdateDelay);
            var displays = _cache.Values.ToList();
            foreach (var display in displays)
            {
                try
                {
                    if (display.Player?.State != PlayerState.Playing) continue;
                    display.UpdateProgress();
                    display.UpdateMessageComponents();
                    await display.UpdateControlMessage(true);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            await waitCycle;
        }
        // ReSharper disable once FunctionNeverReturns
    }
}