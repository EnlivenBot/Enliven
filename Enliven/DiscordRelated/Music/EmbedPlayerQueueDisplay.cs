using System;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Bot.DiscordRelated.MessageComponents;
using Common;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music.Controller;
using Common.Music.Players;
using Common.Utils;
using Discord;

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerQueueDisplay : PlayerDisplayBase {
        private ILocalizationProvider _loc;
        private IMessageChannel _targetChannel;

        public EmbedPlayerQueueDisplay(IMessageChannel targetChannel, ILocalizationProvider loc, MessageComponentService messageComponentService) {
            _loc = loc;
            _messageComponentService = messageComponentService;
            _targetChannel = targetChannel;
        }

        private PaginatedMessage _paginatedMessage = null!;
        private Disposables? _subscribers;
        private MessageComponentService _messageComponentService;

        public override async Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer) {
            var paginatedAppearanceOptions = new PaginatedAppearanceOptions {Timeout = TimeSpan.FromMinutes(1)};
            _paginatedMessage = new PaginatedMessage(paginatedAppearanceOptions, _targetChannel, _loc, _messageComponentService) {
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