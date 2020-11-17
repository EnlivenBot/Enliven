using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Common;
using Common.Config;
using Common.Music.Players;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerDisplayProvider {
        private ConcurrentDictionary<string, EmbedPlayerDisplay> _cache = new ConcurrentDictionary<string, EmbedPlayerDisplay>();
        private IGuildConfigProvider _guildConfigProvider;
        private DiscordShardedClient _client;
        private CommandHandlerService _commandHandlerService;
        
        private readonly Thread UpdateThread;

        public EmbedPlayerDisplayProvider(DiscordShardedClient client, IGuildConfigProvider guildConfigProvider, CommandHandlerService commandHandlerService) {
            _commandHandlerService = commandHandlerService;
            _client = client;
            _guildConfigProvider = guildConfigProvider;
            UpdateThread = new Thread(UpdateCycle) {Priority = ThreadPriority.BelowNormal};
            UpdateThread.Start();
        }

        public EmbedPlayerDisplay Provide(ITextChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
            return ProvideInternal($"guild-{channel.GuildId}", channel, finalLavalinkPlayer);
        }

        private EmbedPlayerDisplay ProvideInternal(string id, ITextChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
            var embedPlayerDisplay = _cache.GetOrAdd(id, s => {
                var guildConfig = _guildConfigProvider.Get(channel.GuildId);
                var display = new EmbedPlayerDisplay(channel, _client, guildConfig.Loc, _commandHandlerService, guildConfig.PrefixProvider);
                
                display.Disposed.Subscribe(playerDisplay => _cache.TryRemove(id, out _));
                finalLavalinkPlayer.AttachDisplay(display);

                display.Initialize(finalLavalinkPlayer);
                
                return display;
            });
            return embedPlayerDisplay;
        }
        
        private void UpdateCycle() {
            while (true) {
                var waitCycle = Task.Delay(Constants.PlayerEmbedUpdateDelay);
                var displays = _cache.Values.ToList();
                foreach (var display in displays) {
                    try {
                        display.UpdateProgress();
                        display.UpdateControlMessage().Wait();
                    }
                    catch (Exception) {
                        // ignored
                    }
                }

                waitCycle.GetAwaiter().GetResult();
            }
            // ReSharper disable once FunctionNeverReturns
        }
    }
}