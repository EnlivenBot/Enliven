using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.DiscordRelated.MessageComponents;
using Common.Config;
using Common.Localization.Providers;
using Common.Music.Players;
using Discord;

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerEffectsDisplayProvider {
        private ConcurrentDictionary<IMessageChannel, EmbedPlayerEffectsDisplay> _cache = new ConcurrentDictionary<IMessageChannel, EmbedPlayerEffectsDisplay>();
        private IGuildConfigProvider _guildConfigProvider;
        private MessageComponentService _messageComponentService;

        public EmbedPlayerEffectsDisplayProvider(IGuildConfigProvider guildConfigProvider, MessageComponentService messageComponentService) {
            _messageComponentService = messageComponentService;
            _guildConfigProvider = guildConfigProvider;
        }

        public EmbedPlayerEffectsDisplay? Get(IMessageChannel channel) {
            return _cache.TryGetValue(channel, out var display) ? display : null;
        }

        public Task<EmbedPlayerEffectsDisplay> CreateOrUpdateQueueDisplay(IMessageChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
            return ProvideInternal(channel, finalLavalinkPlayer);
        }

        private async Task<EmbedPlayerEffectsDisplay> ProvideInternal(IMessageChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
            if (finalLavalinkPlayer.IsShutdowned) throw new InvalidOperationException("You try to provide display for shutdowned player");
            var display = _cache.GetOrAdd(channel, messageChannel => {
                ILocalizationProvider loc;
                if (channel is ITextChannel textChannel) {
                    var guildConfig = _guildConfigProvider.Get(textChannel.GuildId);
                    loc = guildConfig.Loc;
                }
                else {
                    loc = LangLocalizationProvider.EnglishLocalizationProvider;
                }
                var embedPlayerQueueDisplay = new EmbedPlayerEffectsDisplay(channel, loc, _messageComponentService);
                _ = embedPlayerQueueDisplay.Initialize(finalLavalinkPlayer);
                return embedPlayerQueueDisplay;
            });
            if (!display.IsShutdowned && await display.EnsureCorrectnessAsync()) return display;
            _cache.Remove(channel, out _);
            return await ProvideInternal(channel, finalLavalinkPlayer);
        }
    }
}