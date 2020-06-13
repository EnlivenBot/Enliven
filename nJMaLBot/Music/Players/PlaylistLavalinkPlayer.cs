using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Utilities;
using Bot.Utilities.Emoji;
using Lavalink4NET.Decoding;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Tyrrrz.Extensions;

namespace Bot.Music.Players {
    public class PlaylistLavalinkPlayer : AdvancedLavalinkPlayer {
        private int _currentTrackIndex;

        // ReSharper disable once UnusedParameter.Local
        public PlaylistLavalinkPlayer(ulong guildId) : base(guildId) {
            Playlist = new LavalinkPlaylist();
            Playlist.Update += (sender, args) => {
                UpdateCurrentTrackIndex();
                QueuePages = null;
                QueueDeprecated?.Invoke(this, EventArgs.Empty);
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

        private List<string> QueuePages { get; set; }
        public event EventHandler QueueDeprecated;
        public string LoadFailedId = "";
        public int LoadFailedRemoves = 0;

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
                    await PlayAsync(CurrentTrack, false, TrackPosition);
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

        public override Task Shutdown(string reason, bool needSave = true) {
            Playlist.Clear();
            CurrentTrackIndex = 0;
            return base.Shutdown(reason, needSave);
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
            catch (Exception e) {
                CurrentTrackIndex = Playlist.IndexOf(CurrentTrack);
            }
        }

        public List<string> GetQueuePages() {
            if (QueuePages == null) {
                QueuePages = new List<string>();
                var stringBuilder = new StringBuilder();
                for (var i = 0; i < Playlist.Count; i++) {
                    var text = (CurrentTrackIndex == i ? "@" : " ") + $"{i}: {Playlist[i].Title}\n";
                    if (stringBuilder.Length + text.Length > 2000) {
                        QueuePages.Add(stringBuilder.ToString());
                        stringBuilder.Clear();
                    }

                    stringBuilder.Append(text);
                }

                QueuePages.Add(stringBuilder.ToString());
            }

            return QueuePages.ToList();
        }

        private readonly SemaphoreSlim _enqueueLock = new SemaphoreSlim(1);

        public virtual async Task TryEnqueue(IEnumerable<LavalinkTrack> tracks, string author, bool enqueue = true) {
            await _enqueueLock.WaitAsync();
            try {
                var lavalinkTracks = tracks.ToList();
                var authoredTracks = lavalinkTracks.Take(2000 - Playlist.Tracks.Count)
                                                   .Select(track => AuthoredLavalinkTrack.FromLavalinkTrack(track, author)).ToList();

                await Enqueue(authoredTracks, enqueue);

                var ignoredTracksCount = lavalinkTracks.Count - authoredTracks.Count;
                if (ignoredTracksCount != 0) {
                    await OnTrackLimitExceed(author, ignoredTracksCount);
                }
            }
            finally {
                _enqueueLock.Release();
            }
        }

        public virtual async Task Enqueue(List<AuthoredLavalinkTrack> tracks, bool enqueue) {
            if (tracks.Any()) {
                await PlayAsync(tracks.First(), enqueue);

                if (tracks.Count > 1) {
                    Playlist.AddRange(tracks.Skip(1));
                }
            }
        }

        public virtual Task OnTrackLimitExceed(string author, int count) {
            return Task.CompletedTask;
        }

        public virtual void WriteToQueueHistory(string entry, bool background = false) {
            
        }
    }

    public enum LoopingState {
        One,
        All,
        Off
    }
}