using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Decoding;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Newtonsoft.Json;

namespace Bot.Music.Players {
    public class PlaylistLavalinkPlayer : AdvancedLavalinkPlayer {
        private int _currentTrackIndex;

        // ReSharper disable once UnusedParameter.Local
        public PlaylistLavalinkPlayer(ulong guildId) : base(guildId) {
            Playlist = new LavalinkPlaylist();
            Playlist.Update += (sender, args) => { UpdateCurrentTrackIndex(); };
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
            var oldTrackIndex = CurrentTrackIndex;
            if (CurrentTrack != null) {
                CommandHandler.RegisterMusicTime(TrackPosition);
            }

            if (eventArgs.Reason != TrackEndReason.Replaced) await base.OnTrackEndAsync(eventArgs);
            if (eventArgs.MayStartNext || eventArgs.Reason == TrackEndReason.LoadFailed) await SkipAsync();
            if (eventArgs.Reason == TrackEndReason.LoadFailed) Playlist.RemoveAt(oldTrackIndex);
        }

        public virtual async Task<int> PlayAsync(LavalinkTrack track, bool enqueue, TimeSpan? startTime = null, TimeSpan? endTime = null,
                                                 bool noReplace = false) {
            EnsureNotDestroyed();
            EnsureConnected();
            if (enqueue) Playlist.Add(track);
            if (enqueue && State == PlayerState.Playing) return Playlist.Count;
            await base.PlayAsync(track, startTime, endTime, noReplace);
            UpdateCurrentTrackIndex();
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
            if ((force || LoopingState == LoopingState.All) && CurrentTrackIndex > Playlist.Count - 1) CurrentTrackIndex = 0;
            if (force && CurrentTrackIndex < 0) CurrentTrackIndex = Playlist.Count - 1;

            if (Playlist.TryGetValue(CurrentTrackIndex, out var track)) {
                await PlayAsync(track, false, new TimeSpan?(), new TimeSpan?());
            }
        }

        public override async Task DisconnectAsync() {
            await base.DisconnectAsync();
        }

        public override void Dispose() {
            Playlist.Clear();
            CurrentTrackIndex = 0;
            base.Dispose();
        }

        public virtual ExportPlaylist ExportPlaylist(ExportPlaylistOptions options) {
            var exportPlaylist = new ExportPlaylist {Tracks = Playlist.Select(track => track.Identifier).ToList()};
            if (options != ExportPlaylistOptions.IgnoreTrackIndex) {
                exportPlaylist.TrackIndex = CurrentTrackIndex;
            }

            if (options == ExportPlaylistOptions.AllData) {
                exportPlaylist.TrackPosition = TrackPosition;
            }

            return exportPlaylist;
        }

        public virtual async Task ImportPlaylist(ExportPlaylist playlist, ImportPlaylistOptions options, string requester) {
            var tracks = playlist.Tracks.Select(s => TrackDecoder.DecodeTrack(s))
                                 .Select(track => AuthoredLavalinkTrack.FromLavalinkTrack(track, requester)).ToList();
            if (options == ImportPlaylistOptions.Replace) {
                try {
                    await StopAsync();
                }
                catch (Exception) {
                    // ignored
                }

                if (!Playlist.IsEmpty) {
                    Playlist.Clear();
                }
            }


            Playlist.AddRange(tracks);

            if (options != ImportPlaylistOptions.JustAdd) {
                await PlayAsync(playlist.TrackIndex == -1 ? tracks.First() : tracks[playlist.TrackIndex], false, playlist.TrackPosition);
            }
        }

        public void UpdateCurrentTrackIndex() {
            if (CurrentTrack == null) return;
            try {
                if (CurrentTrack.Identifier == Playlist[CurrentTrackIndex].Identifier) return;
                CurrentTrackIndex = Playlist.IndexOf(CurrentTrack);
            }
            catch (Exception e) {
                CurrentTrackIndex = Playlist.IndexOf(CurrentTrack);
            }
        }
    }

    public enum LoopingState {
        One,
        All,
        Off
    }
}