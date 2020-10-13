using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Music.Tracks;
using Bot.Music;
using Bot.Utilities;
using Lavalink4NET;
using Lavalink4NET.Decoding;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using LiteDB;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.Music {
    public class PlaylistLavalinkPlayer : AdvancedLavalinkPlayer {
        private int _currentTrackIndex;

        // ReSharper disable once UnusedParameter.Local
        public PlaylistLavalinkPlayer(ulong guildId) : base(guildId) {
            Playlist = new LavalinkPlaylist();
            Playlist.Update += (sender, args) => { UpdateCurrentTrackIndex(); };
        }

        public LoopingState LoopingState { get; set; } = LoopingState.Off;

        public LavalinkPlaylist Playlist { get; }

        public event EventHandler<int> CurrentTrackIndexChange = null!;

        public int CurrentTrackIndex {
            get => _currentTrackIndex;
            private set {
                var notify = _currentTrackIndex != value;
                _currentTrackIndex = value;
                if (notify)
                    CurrentTrackIndexChange?.Invoke(null, value);
            }
        }

        public string? LoadFailedId = "";
        public int LoadFailedRemoves;

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            var oldTrackIndex = CurrentTrackIndex;
            if (CurrentTrack != null) {
                CommandHandler.RegisterMusicTime(TrackPosition);
            }

            if (eventArgs.Reason != TrackEndReason.Replaced) await base.OnTrackEndAsync(eventArgs);
            if (eventArgs.Reason == TrackEndReason.LoadFailed) {
                if (LoadFailedId == CurrentTrack?.Identifier) {
                    await SkipAsync();
                    Playlist.RemoveAt(oldTrackIndex);
                    LoadFailedRemoves++;
                }
                else {
                    LoadFailedId = CurrentTrack?.Identifier;
                    await PlayAsync(CurrentTrack!, false, TrackPosition);
                }
            }
            else {
                LoadFailedRemoves = 0;
            }

            if (LoadFailedRemoves > 2) {
                try {
                    var currentNode = MusicUtils.Cluster.GetServingNode(Guild.Id);
                    var newNode = MusicUtils.Cluster.Nodes.Where(node => node.IsConnected).Where(node => node != currentNode).RandomOrDefault();
                    if (newNode != null) {
                        await currentNode.MovePlayerAsync(this, newNode);
                        WriteToQueueHistory(Loc.Get("MusicQueues.NodeChanged").Format("SYSTEM", newNode.Label));
                        await NodeChanged(newNode);
                    }
                }
                finally {
                    LoadFailedRemoves = 0;
                }
            }

            if (eventArgs.MayStartNext && eventArgs.Reason != TrackEndReason.LoadFailed) {
                await SkipAsync();
            }
        }

        public virtual Task NodeChanged(LavalinkNode? node = null) {
            return Task.CompletedTask;
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
                await PlayAsync(track!, false, new TimeSpan?(), new TimeSpan?());
            }
        }

        public override async Task DisconnectAsync() {
            await base.DisconnectAsync();
        }

        public virtual ExportPlaylist ExportPlaylist(ExportPlaylistOptions options) {
            var exportPlaylist = new ExportPlaylist {Tracks = Playlist.Select(track => track.Identifier).ToList()};
            if (options != ExportPlaylistOptions.IgnoreTrackIndex) {
                exportPlaylist.TrackIndex = CurrentTrackIndex.Normalize(0, Playlist.Count - 1);
            }

            if (options == ExportPlaylistOptions.AllData) {
                exportPlaylist.TrackPosition = TrackPosition;
            }

            return exportPlaylist;
        }

        public virtual async Task ImportPlaylist(ExportPlaylist playlist, ImportPlaylistOptions options, string requester) {
            if (Playlist.Count + playlist.Tracks.Count > 10000) {
                return;
            }

            var tracks = playlist.Tracks.Select(s => TrackDecoder.DecodeTrack(s))
                                 .Select(track => new AuthoredTrack(track, requester)).ToList();
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
                var track = playlist.TrackIndex == -1 ? tracks.First() : tracks[playlist.TrackIndex.Normalize(0, playlist.Tracks.Count - 1)];
                var position = playlist.TrackPosition;
                if (position != null && position.Value > track.Duration) {
                    position = TimeSpan.Zero;
                }

                await PlayAsync(track, false, position);
            }
            else if (State == PlayerState.NotPlaying) {
                await PlayAsync(Playlist[0], false);
            }
        }

        public void UpdateCurrentTrackIndex() {
            if (CurrentTrack == null) return;
            try {
                if (CurrentTrack.Identifier == Playlist[CurrentTrackIndex].Identifier) return;
                CurrentTrackIndex = Playlist.IndexOf(CurrentTrack);
            }
            catch (Exception) {
                CurrentTrackIndex = Playlist.IndexOf(CurrentTrack);
            }
        }

        private readonly SemaphoreSlim _enqueueLock = new SemaphoreSlim(1);

        public virtual async Task TryEnqueue(IEnumerable<LavalinkTrack> tracks, string author, int index) {
            await _enqueueLock.WaitAsync();
            try {
                var lavalinkTracks = tracks.ToList();
                var authoredTracks = lavalinkTracks.Take(Constants.MaxTracksCount - Playlist.Tracks.Count)
                                                   .Select(track => new AuthoredTrack(track, author)).ToList();

                await Enqueue(authoredTracks, index);

                var ignoredTracksCount = lavalinkTracks.Count - authoredTracks.Count;
                if (ignoredTracksCount != 0) {
                    await OnTrackLimitExceed(author, ignoredTracksCount);
                }
            }
            finally {
                _enqueueLock.Release();
            }
        }

        public virtual async Task Enqueue(List<AuthoredTrack> tracks, int position = -1) {
            var localTracks = tracks.ToList();
            if (localTracks.Any()) {
                if (position == -1) {
                    if (State != PlayerState.Paused) {
                        await PlayAsync(localTracks.First(), true);
                        localTracks.RemoveAt(0);
                    }

                    Playlist.AddRange(localTracks);
                }
                else {
                    Playlist.InsertRange(position, localTracks);
                    if (State == PlayerState.NotPlaying) {
                        await PlayAsync(localTracks.First(), false, null, null, true);
                    }
                }
            }
        }

        public virtual Task OnTrackLimitExceed(string author, int count) {
            return Task.CompletedTask;
        }

        public override PlayerShutdownParameters GetPlayerShutdownParameters(PlayerShutdownParameters parameters) {
            parameters.Playlist = Playlist;
            if (parameters.NeedSave && parameters.StoredPlaylist != null) {
                parameters.StoredPlaylist = ExportPlaylist(ExportPlaylistOptions.AllData).StorePlaylist("a" + ObjectId.NewObjectId(), 0);
            }
            
            return base.GetPlayerShutdownParameters(parameters);
        }
    }

    public enum LoopingState {
        One,
        All,
        Off
    }
}