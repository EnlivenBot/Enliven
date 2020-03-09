using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;

namespace Bot.Music.Players {
    public class PlaylistLavalinkPlayer : AdvancedLavalinkPlayer {
        private int _currentTrackIndex;

        // ReSharper disable once UnusedParameter.Local
        public PlaylistLavalinkPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop)
            : base(lavalinkSocket, client, guildId, false) {
            Playlist = new LavalinkPlaylist();
            Playlist.Update += (sender, args) => {
                if (CurrentTrack != null) CurrentTrackIndex = Playlist.IndexOf(CurrentTrack);
            };
        }

        public LoopingState LoopingState { get; set; } = LoopingState.Off;
        public LavalinkPlaylist Playlist { get; }

        public event EventHandler<int> CurrentTrackIndexChange;

        public int CurrentTrackIndex {
            get => _currentTrackIndex;
            private set {
                var notify = _currentTrackIndex != value;
                _currentTrackIndex = value;
                if (notify)
                    CurrentTrackIndexChange?.Invoke(null, value);
            }
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            if (eventArgs.Reason == TrackEndReason.LoadFailed) Playlist.Remove(CurrentTrack);
            if (CurrentTrack != null) {
                CommandHandler.RegisterMusicTime(TrackPosition);
            }
            if (eventArgs.Reason != TrackEndReason.Replaced) await base.OnTrackEndAsync(eventArgs);
            if (eventArgs.MayStartNext || eventArgs.Reason == TrackEndReason.LoadFailed) await ContinueOnTrackEnd();
        }

        private async Task ContinueOnTrackEnd() {
            if (LoopingState == LoopingState.One && CurrentTrack != null) {
                await PlayAsync(CurrentTrack, false);
                return;
            }

            if (Playlist.TryGetValue(CurrentTrackIndex + 1, out var track)) {
                await PlayAsync(track, false);
                return;
            }

            if (LoopingState == LoopingState.All) {
                await PlayAsync(Playlist.First(), false);
            }
        }

        public virtual async Task<int> PlayAsync(LavalinkTrack track, bool enqueue, TimeSpan? startTime = null, TimeSpan? endTime = null,
                                                 bool noReplace = false) {
            EnsureNotDestroyed();
            EnsureConnected();
            if (enqueue) Playlist.Add(track);
            if (enqueue && State == PlayerState.Playing) return Playlist.Count;
            CurrentTrackIndex = Playlist.IndexOf(track);
            await base.PlayAsync(track, startTime, endTime, noReplace);
            return 0;
        }

        public virtual async Task SkipAsync(int count = 1, bool force = false) {
            EnsureNotDestroyed();
            EnsureConnected();
            if (!force && LoopingState == LoopingState.One && CurrentTrack != null) {
                await PlayAsync(CurrentTrack, false, new TimeSpan?(), new TimeSpan?());
                return;
            }

            if (Playlist.IsEmpty)
                return;

            CurrentTrackIndex += count;
            CurrentTrackIndex = Math.Min(Math.Max(CurrentTrackIndex, 0), Playlist.Count - 1);

            if (Playlist.TryGetValue(CurrentTrackIndex, out var track)) {
                await PlayAsync(track, false, new TimeSpan?(), new TimeSpan?());
                return;
            }

            if (LoopingState != LoopingState.All) {
                await DisconnectAsync();
                return;
            }

            CurrentTrackIndex = 0;
            await PlayAsync(Playlist.First(), false);
        }

        public virtual void Cleanup() {
            Playlist.Clear();
            CurrentTrackIndex = 0;
        }

        public override async Task DisconnectAsync() {
            Cleanup();
            await base.DisconnectAsync();
        }

        public override Task StopAsync(bool disconnect = false) {
            if (disconnect) Cleanup();
            return base.StopAsync(disconnect);
        }
    }

    public enum LoopingState {
        One,
        All,
        Off
    }
}