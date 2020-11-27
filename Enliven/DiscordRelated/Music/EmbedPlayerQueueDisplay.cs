using System;
using System.Linq;
using System.Threading.Tasks;
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

        public EmbedPlayerQueueDisplay(IMessageChannel targetChannel, ILocalizationProvider loc) {
            _loc = loc;
            _targetChannel = targetChannel;
        }

        private PaginatedMessage _paginatedMessage = null!;
        private Disposables? _subscribers;

        public override async Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer) {
            var paginatedAppearanceOptions = new PaginatedAppearanceOptions {Timeout = TimeSpan.FromMinutes(1)};
            _paginatedMessage = new PaginatedMessage(paginatedAppearanceOptions, _targetChannel, _loc) {
                Title = _loc.Get("MusicQueues.QueueTitle"), Color = Color.Gold
            };
            await base.Initialize(finalLavalinkPlayer);
            await _paginatedMessage.Resend();
        }

        private void UpdatePages() {
            _paginatedMessage?.SetPages(string.Join("\n",
                    Player.Playlist.Select((track, i) =>
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
                Player.Shutdown.Subscribe(entry => Shutdown(null, null))
            );
            return Task.CompletedTask;
        }

        public override Task Shutdown(IEntry? header, IEntry? body) {
            base.Shutdown(header!, body!);
            _subscribers?.Dispose();
            _paginatedMessage.StopAndClear();
            return Task.CompletedTask;
        }

        public override Task LeaveNotification(IEntry? header, IEntry? body) {
            return Task.CompletedTask;
        }
    }
}