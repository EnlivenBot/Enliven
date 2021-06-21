using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.MessageComponents;
using Common;
using Common.Config;
using Common.Music.Players;
using Discord;
using Discord.WebSocket;
using Lavalink4NET.Player;
using Newtonsoft.Json;
using NLog;

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerDisplayProvider {
        private ConcurrentDictionary<string, EmbedPlayerDisplay> _cache = new ConcurrentDictionary<string, EmbedPlayerDisplay>();
        private IGuildConfigProvider _guildConfigProvider;
        private DiscordShardedClient _client;
        private CommandHandlerService _commandHandlerService;
        private ILogger _logger;
        private MessageComponentService _messageComponentService;

        public EmbedPlayerDisplayProvider(DiscordShardedClient client, IGuildConfigProvider guildConfigProvider,
                                          CommandHandlerService commandHandlerService, MessageComponentService messageComponentService,
                                          ILogger logger) {
            _messageComponentService = messageComponentService;
            _commandHandlerService = commandHandlerService;
            _logger = logger;
            _client = client;
            _guildConfigProvider = guildConfigProvider;
            new Task(UpdateCycle, TaskCreationOptions.LongRunning).Start();
        }

        public EmbedPlayerDisplay? Get(string id) {
            return _cache.TryGetValue(id, out var display) ? display : null;
        }

        public EmbedPlayerDisplay? Get(ITextChannel channel) {
            return Get($"guild-{channel.GuildId}");
        }

        public EmbedPlayerDisplay Provide(ITextChannel channel, FinalLavalinkPlayer finalLavalinkPlayer) {
            return ProvideInternal($"guild-{channel.GuildId}", channel, finalLavalinkPlayer);
        }

        private EmbedPlayerDisplay ProvideInternal(string id, ITextChannel channel, FinalLavalinkPlayer finalLavalinkPlayer, int recursiveCount = 0) {
            var embedPlayerDisplay = _cache.GetOrAdd(id, s => {
                var guildConfig = _guildConfigProvider.Get(channel.GuildId);
                var display = new EmbedPlayerDisplay(channel, _client, guildConfig.Loc, _commandHandlerService, guildConfig.PrefixProvider, _messageComponentService);

                _ = display.Initialize(finalLavalinkPlayer);

                return display;
            });
            if (!embedPlayerDisplay.IsShutdowned && !embedPlayerDisplay.Player?.IsShutdowned != false) return embedPlayerDisplay;
            _cache.TryRemove(id, out _);
            if (recursiveCount <= 1) return ProvideInternal(id, channel, finalLavalinkPlayer, ++recursiveCount);
            _logger.Fatal("Provider recursive call. Provider: {data}",
                JsonConvert.SerializeObject(embedPlayerDisplay, Formatting.None,
                    new JsonSerializerSettings() {ReferenceLoopHandling = ReferenceLoopHandling.Ignore}));
            return embedPlayerDisplay;
        }

        private async void UpdateCycle() {
            while (true) {
                var waitCycle = Task.Delay(Constants.PlayerEmbedUpdateDelay);
                var displays = _cache.Values.ToList();
                foreach (var display in displays) {
                    try {
                        if (display.Player.State != PlayerState.Playing) continue;
                        display.UpdateProgress();
                        display.UpdateMessageComponents();
                        await display.UpdateControlMessage();
                    }
                    catch (Exception) {
                        // ignored
                    }
                }

                await waitCycle;
            }
            // ReSharper disable once FunctionNeverReturns
        }
    }
}