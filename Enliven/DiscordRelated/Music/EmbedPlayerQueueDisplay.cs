using System;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Bot.DiscordRelated.MessageComponents;
using Bot.Utilities.Collector;
using Common;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music.Controller;
using Common.Music.Players;
using Common.Utils;
using Discord;

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerQueueDisplay : PlayerDisplayBase {
        private readonly ILocalizationProvider _loc;
        private readonly IMessageChannel _targetChannel;
        private readonly MessageComponentService _messageComponentService;
        private readonly CollectorService _collectorService;
        private readonly IDiscordClient _discordClient;

        public EmbedPlayerQueueDisplay(IMessageChannel targetChannel, ILocalizationProvider loc, MessageComponentService messageComponentService, CollectorService collectorService, IDiscordClient discordClient) {
            _loc = loc;
            _messageComponentService = messageComponentService;
            _collectorService = collectorService;
            _discordClient = discordClient;
            _targetChannel = targetChannel;
        }

        private PaginatedMessage _paginatedMessage = null!;
        private Disposables? _subscribers;

        public override async Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer) {
            var paginatedAppearanceOptions = new PaginatedAppearanceOptions { Timeout = TimeSpan.FromMinutes(1) };
            _paginatedMessage = new PaginatedMessage(paginatedAppearanceOptions, _targetChannel, _loc, _messageComponentService, _collectorService, _discordClient) {
                Title = _loc.Get("MusicQueues.QueueTitle"), Color = Color.Gold
            };
            _paginatedMessage.Disposed.Subscribe(base2 => ExecuteShutdown(null, null));
            await base.Initialize(finalLavalinkPlayer);
            await _paginatedMessage.Resend();
        }

        private void UpdatePages() {
            _paginatedMessage?.SetPages(string.Join("\n",
                    Player!.Playlist.Select((track, i) =>
                        (Player.CurrentTrackIndex == i ? "@" : " ")
                      + $"{i + 1}: {MusicController.EscapeTrack(Player.Playlist[i].Title)}")),
                "```py\n{0}```", 50);
        }


        public override Task ChangePlayer(FinalLavalinkPlayer newPlayer) {
            base.ChangePlayer(newPlayer);
            UpdatePages();
            _subscribers?.Dispose();
            _subscribers = new Disposables(
                Player.Playlist.Changed.Subscribe(playlist => UpdatePages()),
                Player.CurrentTrackIndexChanged.Subscribe(i => UpdatePages()),
                Player.ShutdownTask.ToObservable().SubscribeAsync(snapshot => ExecuteShutdown(null, null))
            );
            return Task.CompletedTask;
        }

        public override async Task ExecuteShutdown(IEntry? header, IEntry? body) {
            await base.ExecuteShutdown(header!, body!);
            _subscribers?.Dispose();
            _paginatedMessage.Dispose();
        }

        public override Task LeaveNotification(IEntry? header, IEntry? body) {
            return Task.CompletedTask;
        }
    }
}