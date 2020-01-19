using System;
using System.Threading.Tasks;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;

namespace Bot.Music.Players {
    public class PlaylistLavalinkPlayer : LavalinkPlayer {
        private readonly bool _disconnectOnStop;
        private int _currentTrackIndex = 0;

        public PlaylistLavalinkPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop)
            : base(lavalinkSocket, client, guildId, false) {
            Playlist = new LavalinkPlaylist();
            _disconnectOnStop = disconnectOnStop;
        }

        public LoopingState LoopingState { get; set; } = LoopingState.No;
        public LavalinkPlaylist Playlist { get; }

        public event EventHandler<int> CurrentTrackIndexChange;
        public int CurrentTrackIndex {
            get => _currentTrackIndex;
            set {
                _currentTrackIndex = value;
                CurrentTrackIndexChange.Invoke(null, value);
            }
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            if (eventArgs.MayStartNext) await SkipAsync();
            await base.OnTrackEndAsync(eventArgs);
        }

        public virtual Task<int> PlayAsync(LavalinkTrack track, TimeSpan? startTime = null, TimeSpan? endTime = null, bool noReplace = false) {
            return PlayAsync(track, true, startTime, endTime, noReplace);
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

        // public virtual async Task PlayTopAsync(LavalinkTrack track) {
        //     EnsureNotDestroyed();
        //     if (track == null) throw new ArgumentNullException(nameof(track));
        //     if (State == PlayerState.NotPlaying) {
        //         var num = await PlayAsync(track, false, new TimeSpan?(), new TimeSpan?());
        //     }
        //     else Playlist.Insert(0, track);
        // }
        //
        // public virtual async Task<bool> PushTrackAsync(LavalinkTrack track, bool push = false) {
        //     if (State == PlayerState.NotPlaying) {
        //         if (push) return false;
        //         var num = await PlayAsync(track, false, new TimeSpan?(), new TimeSpan?());
        //         return false;
        //     }
        //
        //     var track1 = CurrentTrack.WithPosition(TrackPosition);
        //     Playlist.Add(track1);
        //     var num1 = await PlayAsync(track, false, new TimeSpan?(), new TimeSpan?());
        //     return true;
        // }

        public virtual async Task SkipAsync(int count = 1) {
            EnsureNotDestroyed();
            EnsureConnected();
            if (LoopingState == LoopingState.LoopingTrack && CurrentTrack != null) {
                await PlayAsync(CurrentTrack, false, new TimeSpan?(), new TimeSpan?());
                return;
            }

            if (Playlist.IsEmpty) {
                if (_disconnectOnStop) await DisconnectAsync();
                return;
            }

            CurrentTrackIndex += count;
            if (CurrentTrackIndex < 0) CurrentTrackIndex = Playlist.Count - 1;
            
            if (Playlist.TryGetValue(CurrentTrackIndex, out var track)) {
                await PlayAsync(track, false, new TimeSpan?(), new TimeSpan?());
                return;
            }

            if (LoopingState != LoopingState.LoopingPlaylist) {
                await DisconnectAsync();
                return;
            }

            CurrentTrackIndex = 0;
            await PlayAsync(Playlist[0], false);
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
        LoopingTrack,
        LoopingPlaylist,
        No
    }
}