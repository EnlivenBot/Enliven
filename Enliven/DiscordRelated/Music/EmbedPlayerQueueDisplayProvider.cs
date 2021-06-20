using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.MessageComponents;
using Common.Config;
using Common.Localization.Providers;
using Common.Music.Players;
using Discord;
using Newtonsoft.Json;
using NLog;

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerQueueDisplayProvider {
        private ConcurrentDictionary<IMessageChannel, EmbedPlayerQueueDisplay> _cache = new ConcurrentDictionary<IMessageChannel, EmbedPlayerQueueDisplay>();
        private IGuildConfigProvider _guildConfigProvider;
        private ILogger _logger;
        private MessageComponentService _messageComponentService;

        public EmbedPlayerQueueDisplayProvider(IGuildConfigProvider guildConfigProvider, MessageComponentService messageComponentService, ILogger logger) {
            _messageComponentService = messageComponentService;
            _logger = logger;
            _guildConfigProvider = guildConfigProvider;
        }

        public EmbedPlayerQueueDisplay? Get(IMessageChannel channel) {
            return _cache.TryGetValue(channel, out var display) ? display : null;
        }

        public EmbedPlayerQueueDisplay CreateOrUpdateQueueDisplay(IMessageChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
            return ProvideInternal(channel, finalLavalinkPlayer);
        }

        private EmbedPlayerQueueDisplay ProvideInternal(IMessageChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
            var display = _cache.GetOrAdd(channel, messageChannel => {
                ILocalizationProvider loc;
                if (channel is ITextChannel textChannel) {
                    var guildConfig = _guildConfigProvider.Get(textChannel.GuildId);
                    loc = guildConfig.Loc;
                }
                else {
                    loc = new LangLocalizationProvider("en");
                }
                var embedPlayerQueueDisplay = new EmbedPlayerQueueDisplay(channel, loc, _messageComponentService);
                _ = embedPlayerQueueDisplay.Initialize(finalLavalinkPlayer);
                return embedPlayerQueueDisplay;
            });
            if (!display.IsShutdowned) return display;
            _cache.Remove(channel, out _);
            return ProvideInternal(channel, finalLavalinkPlayer);
        }
    }
}