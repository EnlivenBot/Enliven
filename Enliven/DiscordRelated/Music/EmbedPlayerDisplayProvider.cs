using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
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
    public class EmbedPlayerDisplayProvider : IService, IDisposable {
        private readonly ConcurrentDictionary<string, EmbedPlayerDisplay> _cache = new();
        private readonly IGuildConfigProvider _guildConfigProvider;
        private readonly EnlivenShardedClient _client;
        private readonly CommandHandlerService _commandHandlerService;
        private readonly ILogger _logger;
        private readonly MessageComponentService _messageComponentService;
        private IDisposable? _restoreStoppedHandled;

        public EmbedPlayerDisplayProvider(EnlivenShardedClient client, IGuildConfigProvider guildConfigProvider,
                                          CommandHandlerService commandHandlerService, MessageComponentService messageComponentService,
                                          ILogger logger) {
            _messageComponentService = messageComponentService;
            _commandHandlerService = commandHandlerService;
            _logger = logger;
            _client = client;
            _guildConfigProvider = guildConfigProvider;
        }

        public Task OnPreDiscordStart() {
            _restoreStoppedHandled = _messageComponentService.MessageComponentUse
                .Where(component => component.Data.CustomId == "restoreStoppedPlayer")
                .SubscribeAsync(component => {
                    var guild = (component.Channel as IGuildChannel)?.Guild;
                    if (guild == null) return Task.CompletedTask;
                    var context = new ControllableCommandContext(_client) { Guild = guild, Channel = component.Channel, User = component.User };
                    return _commandHandlerService.ExecuteCommand("resume", context, component.User.Id.ToString());
                });
            new Task(UpdateCycle, TaskCreationOptions.LongRunning).Start();
            return Task.CompletedTask;
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
            if (finalLavalinkPlayer.IsShutdowned) throw new InvalidOperationException("You try to provide display for shutdowned player");
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
                    new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
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

        public void Dispose() {
            _restoreStoppedHandled?.Dispose();
        }
    }
}