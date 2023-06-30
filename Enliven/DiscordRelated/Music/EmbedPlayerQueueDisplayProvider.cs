using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Bot.DiscordRelated.MessageComponents;
using Bot.Utilities.Collector;
using Common.Config;
using Common.Localization.Providers;
using Common.Music.Players;
using Discord;

namespace Bot.DiscordRelated.Music; 

public class EmbedPlayerQueueDisplayProvider {
    private readonly ConcurrentDictionary<IMessageChannel, EmbedPlayerQueueDisplay> _cache = new();
    private readonly CollectorService _collectorService;
    private readonly IDiscordClient _discordClient;
    private readonly IGuildConfigProvider _guildConfigProvider;
    private readonly MessageComponentService _messageComponentService;

    public EmbedPlayerQueueDisplayProvider(IGuildConfigProvider guildConfigProvider, MessageComponentService messageComponentService, CollectorService collectorService, IDiscordClient discordClient) {
        _messageComponentService = messageComponentService;
        _collectorService = collectorService;
        _discordClient = discordClient;
        _guildConfigProvider = guildConfigProvider;
    }

    public EmbedPlayerQueueDisplay? Get(IMessageChannel channel) {
        return _cache.TryGetValue(channel, out var display) ? display : null;
    }

    public EmbedPlayerQueueDisplay CreateOrUpdateQueueDisplay(IMessageChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
        return ProvideInternal(channel, finalLavalinkPlayer);
    }

    private EmbedPlayerQueueDisplay ProvideInternal(IMessageChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
        if (finalLavalinkPlayer.IsShutdowned) throw new InvalidOperationException("You try to provide display for shutdowned player");
        var display = _cache.GetOrAdd(channel, messageChannel => {
            ILocalizationProvider loc;
            if (channel is ITextChannel textChannel) {
                var guildConfig = _guildConfigProvider.Get(textChannel.GuildId);
                loc = guildConfig.Loc;
            }
            else
                loc = LangLocalizationProvider.EnglishLocalizationProvider;
            var embedPlayerQueueDisplay = new EmbedPlayerQueueDisplay(channel, loc, _messageComponentService, _collectorService, _discordClient);
            _ = embedPlayerQueueDisplay.Initialize(finalLavalinkPlayer);
            return embedPlayerQueueDisplay;
        });
        if (!display.IsShutdowned) return display;
        _cache.Remove(channel, out _);
        return ProvideInternal(channel, finalLavalinkPlayer);
    }
}