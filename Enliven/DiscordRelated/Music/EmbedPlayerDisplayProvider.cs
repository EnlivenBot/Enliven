using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Interactions.Handlers;
using Bot.Music.Cluster;
using Common;
using Common.Config;
using Discord;
using Lavalink4NET.Artwork;
using Lavalink4NET.Players;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

// ReSharper disable ForCanBeConvertedToForeach

namespace Bot.DiscordRelated.Music;

public sealed class EmbedPlayerDisplayProvider(
    EnlivenShardedClient client,
    IGuildConfigProvider guildConfigProvider,
    CommandHandlerService commandHandlerService,
    MessageComponentInteractionsHandler messageComponentInteractionsHandler,
    ILogger<EmbedPlayerDisplayProvider> logger,
    ILoggerFactory loggerFactory,
    IArtworkService artworkService,
    IEnlivenClusterAudioService clusterAudioService)
    : IService, IDisposable {
    private static TimeSpan InteractionsUpdateDelay { get; set; } = TimeSpan.FromSeconds(1);
    private static TimeSpan MessagesUpdateDelay { get; set; } = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, EmbedPlayerDisplay> _cache = new();
    private readonly CancellationTokenSource _updateCycleCancellationTokenSource = new();

    public Task OnPreDiscordStart() {
        _ = UpdateInteractionsCycle().ObserveException();
        _ = UpdateMessagesCycle().ObserveException();
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
            var guildConfig = guildConfigProvider.Get(channel.GuildId);
            // TODO: Implement proper logger creation
            return new EmbedPlayerDisplay(channel, client, guildConfig.Loc, commandHandlerService,
                messageComponentInteractionsHandler, loggerFactory.CreateLogger<EmbedPlayerDisplay>(),
                artworkService, clusterAudioService);
        });
        if (!embedPlayerDisplay.IsShutdowned && embedPlayerDisplay.Player?.State != PlayerState.Destroyed)
            return embedPlayerDisplay;
        _cache.TryRemove(id, out _);
        if (recursiveCount <= 1) return ProvideInternal(id, channel, ++recursiveCount);
        logger.LogCritical("Provider recursive call. Provider: {Data}",
            JsonConvert.SerializeObject(embedPlayerDisplay, Formatting.None,
                new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
        return embedPlayerDisplay;
    }

    private async Task UpdateInteractionsCycle() {
        var tasks = new List<Task?>();
        while (!_updateCycleCancellationTokenSource.IsCancellationRequested) {
            var waitCycle = Task.Delay(InteractionsUpdateDelay, _updateCycleCancellationTokenSource.Token);
            var displays = _cache.Values.ToList();
            for (var i = 0; i < displays.Count; i++) {
                IEmbedPlayerDisplayBackgroundUpdatable display = displays[i];
                if (display.Player?.State != PlayerState.Playing) continue;
                if (!display.IsInitialized) continue;
                if (!display.UpdateViaInteractions) continue;

                tasks.Add(display.Update());
            }

            try {
                await Task.WhenAll(tasks.WhereNotNull());
            }
            catch (Exception) {
                // ignored
            }

            tasks.Clear();
            await waitCycle;
        }
    }

    private async Task UpdateMessagesCycle() {
        while (!_updateCycleCancellationTokenSource.IsCancellationRequested) {
            var waitCycle = Task.Delay(MessagesUpdateDelay, _updateCycleCancellationTokenSource.Token);
            var displays = _cache.Values.ToList();
            for (var i = 0; i < displays.Count; i++) {
                IEmbedPlayerDisplayBackgroundUpdatable display = displays[i];
                if (display.Player?.State != PlayerState.Playing) continue;
                if (!display.IsInitialized) continue;
                if (display.UpdateViaInteractions) continue;

                try {
                    // TODO Proper update rate when a lot of players
                    // Probably not in a near future hehe
                    await display.Update();
                }
                catch (Exception) {
                    // ignored
                }
            }

            await waitCycle;
        }
    }

    public void Dispose() {
        _updateCycleCancellationTokenSource.Cancel();
    }
}