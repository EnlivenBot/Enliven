using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Common.Config;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Controller;
using Common.Music.Encoders;
using Common.Music.Resolvers;
using Common.Music.Tracks;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using LiteDB;
using Tyrrrz.Extensions;

namespace Common.Music.Players {
    public class PlaylistLavalinkPlayer : AdvancedLavalinkPlayer {
        private static readonly IEntry StartPlayingFailedEntry = new EntryLocalized("Music.StartPlayingFailed");
        private readonly ISubject<int> _currentTrackIndexChanged = new Subject<int>();

        private readonly SemaphoreSlim _enqueueLock = new SemaphoreSlim(1);

        private readonly ISubject<LoopingState> _loopingStateChanged = new Subject<LoopingState>();
        private int _currentTrackIndex;
        private LoopingState _loopingState = LoopingState.Off;
        private IPlaylistProvider _playlistProvider;
        private TrackEncoderUtils _trackEncoderUtils;

        public string? LoadFailedId = "";
        public int LoadFailedRemoves;

        // ReSharper disable once UnusedParameter.Local
        public PlaylistLavalinkPlayer(IMusicController musicController, IGuildConfigProvider guildConfigProvider, IPlaylistProvider playlistProvider,
                                      TrackEncoderUtils trackEncoderUtils) : base(
            musicController, guildConfigProvider) {
            _trackEncoderUtils = trackEncoderUtils;
            _playlistProvider = playlistProvider;
            Playlist.Changed.Subscribe(playlist => UpdateCurrentTrackIndex());
        }

        public LoopingState LoopingState {
            get => _loopingState;
            set {
                _loopingState = value;
                _loopingStateChanged.OnNext(value);
            }
        }

        public IObservable<LoopingState> LoopingStateChanged => _loopingStateChanged;

        public LavalinkPlaylist Playlist { get; } = new LavalinkPlaylist();
        public IObservable<int> CurrentTrackIndexChanged => _currentTrackIndexChanged;

        public int CurrentTrackIndex {
            get => _currentTrackIndex;
            private set {
                if (_currentTrackIndex == value) return;
                _currentTrackIndex = value;
                _currentTrackIndexChanged.OnNext(value);
            }
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            if (eventArgs.Reason == TrackEndReason.Replaced) return;

            var oldTrackIndex = CurrentTrackIndex;
            // if (CurrentTrack != null) {
            //     CommandHandler.RegisterMusicTime(TrackPosition);
            // }

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
                    var cluster = await MusicController.ClusterTask;
                    var currentNode = cluster.GetServingNode(GuildId);
                    var newNode = cluster.Nodes
                        .Where(node => node.IsConnected)
                        .Where(node => node != currentNode)
                        .RandomOrDefault();
                    if (newNode != null) {
                        await currentNode.MovePlayerAsync(this, newNode);
                        WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.NodeChanged", "SYSTEM", newNode.Label ?? "")));
                    }
                }
                finally {
                    LoadFailedRemoves = 0;
                }
            }

            var needEnd = true;
            if (eventArgs.MayStartNext && eventArgs.Reason != TrackEndReason.LoadFailed) {
                needEnd = needEnd && !await SkipAsync();
            }

            if (needEnd) {
                await base.OnTrackEndAsync(eventArgs);
            }
        }
        public virtual async Task PlayAsync(LavalinkTrack track, bool enqueue, TimeSpan? startTime = null, TimeSpan? endTime = null,
                                            bool noReplace = false) {
            EnsureNotDestroyed();
            EnsureConnected();
            if (enqueue) Playlist.Add(track);
            if (enqueue && State == PlayerState.Playing) return;
            try {
                await base.PlayAsync(track, startTime, endTime, noReplace);
            }
            catch (Exception e) when (e is not InvalidOperationException) {
                Logger.Error(e, "An error occurred while trying to start playing track {TrackName}. Track {TrackType} identifier: {TrackIdentifier}", track.Title, track.GetType().Name, track.Identifier);
                _ = OnPlayErrored(track);
            }
            UpdateCurrentTrackIndex();
            if (Playlist.TryGetValue(CurrentTrackIndex + 1, out var nextTrack) && nextTrack is ITrackNeedPrefetch needPrefetchTrack) _ = needPrefetchTrack.PrefetchTrack();
        }

        private async Task OnPlayErrored(LavalinkTrack track) {
            // Some delay to allow return control flow of PlayAsync to caller
            await Task.Delay(100);
            WriteToQueueHistory(StartPlayingFailedEntry.WithArg(track.Title.RemoveNonPrintableChars().SafeSubstring(30) ?? "null"));
            var nextTrack = Playlist.TryGetValue(CurrentTrackIndex + 1, out var _nextTrack)
                ? _nextTrack
                : null;
            Playlist.Remove(track);
            if (nextTrack != null)
                await PlayAsync(nextTrack);
        }

        public virtual async Task<bool> SkipAsync(int count = 1, bool force = false) {
            EnsureNotDestroyed();
            EnsureConnected();
            if (!force && LoopingState == LoopingState.One && CurrentTrack != null) {
                await PlayAsync(CurrentTrack, false);
                return true;
            }

            if (Playlist.IsEmpty)
                return false;

            CurrentTrackIndex += count;
            if ((force || LoopingState == LoopingState.All) && CurrentTrackIndex > Playlist.Count - 1) CurrentTrackIndex = 0;
            if (force && CurrentTrackIndex < 0) CurrentTrackIndex = Playlist.Count - 1;

            if (!Playlist.TryGetValue(CurrentTrackIndex, out var track)) return false;
            await PlayAsync(track!, false);
            return true;
        }

        public override async Task DisconnectAsync() {
            await base.DisconnectAsync();
        }

        public virtual async Task<ExportPlaylist> ExportPlaylist(ExportPlaylistOptions options) {
            var encodedTracks = await Task.WhenAll(Playlist.Select(track => _trackEncoderUtils.Encode(track)));
            var exportPlaylist = new ExportPlaylist { Tracks = encodedTracks.ToList() };
            if (options != ExportPlaylistOptions.IgnoreTrackIndex) {
                exportPlaylist.TrackIndex = CurrentTrackIndex.Normalize(0, Playlist.Count - 1);
            }

            if (options == ExportPlaylistOptions.AllData) {
                exportPlaylist.TrackPosition = Position.Position;
            }

            return exportPlaylist;
        }

        public virtual async Task ImportPlaylist(ExportPlaylist playlist, ImportPlaylistOptions options, string requester) {
            if (Playlist.Count + playlist.Tracks.Count > 10000) {
                WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.PlaylistLoadingLimit", requester, playlist.Tracks.Count,
                    Constants.MaxTracksCount)));
                return;
            }

            var tracks = (await _trackEncoderUtils.BatchDecode(playlist.Tracks)).Select(track => track.AddAuthor(requester)).ToList();
            if (options == ImportPlaylistOptions.Replace) {
                try {
                    await StopAsync();
                    WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.ImportPlayerStop")));
                }
                catch (Exception) {
                    // ignored
                }

                if (!Playlist.IsEmpty) {
                    Playlist.Clear();
                    WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.ClearPlaylist", requester)));
                }
            }

            Playlist.AddRange(tracks);
            WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.AddTracks", requester, tracks.Count)));

            if (options != ImportPlaylistOptions.JustAdd) {
                var track = playlist.TrackIndex == -1 ? tracks.First() : tracks[playlist.TrackIndex.Normalize(0, playlist.Tracks.Count - 1)];
                var position = playlist.TrackPosition;
                if (position != null && position.Value > track.Duration) {
                    position = TimeSpan.Zero;
                }

                await PlayAsync(track, false, position);
                WriteToQueueHistory(new EntryLocalized("MusicQueues.Jumped", requester, CurrentTrackIndex + 1,
                    CurrentTrack!.Title.RemoveNonPrintableChars().SafeSubstring(100, "...")!));
            }
            else if (State == PlayerState.NotPlaying) {
                await PlayAsync(Playlist[0], false);
            }
        }

        protected void UpdateCurrentTrackIndex() {
            if (CurrentTrack == null) return;
            try {
                if (CurrentTrack.Identifier == Playlist[CurrentTrackIndex].Identifier) return;
                CurrentTrackIndex = Playlist.IndexOf(CurrentTrack);
            }
            catch (Exception) {
                CurrentTrackIndex = Playlist.IndexOf(CurrentTrack);
            }
        }

        public virtual async Task TryEnqueue(IEnumerable<MusicResolver> resolvers, string author, int index = -1) {
            var musicResolvers = resolvers.ToList();
            var currentResolverIndex = 0;
            var addedTracks = new List<LavalinkTrack>();
            var historyEntry = new HistoryEntry(new EntryLocalized("Music.ResolvingTracks",
                () => author, () => musicResolvers.Count, () => currentResolverIndex, () => addedTracks.Count));
            WriteToQueueHistory(historyEntry);
            await _enqueueLock.WaitAsync();

            try {
                var isLimitHit = false;
                for (; currentResolverIndex < musicResolvers.Count; currentResolverIndex++) {
                    var musicResolver = musicResolvers[currentResolverIndex];
                    var availableNumberOfTracks = Constants.MaxTracksCount - Playlist.Tracks.Count;
                    if (availableNumberOfTracks <= 0) {
                        isLimitHit = true;
                        break;
                    }

                    historyEntry.Update();
                    var tracks = await musicResolver.GetTracks();
                    var authoredTracks = tracks.Take(availableNumberOfTracks).Select(track => track.AddAuthor(author)).ToList();
                    await Enqueue(authoredTracks, index == -1 ? index : Math.Min(Playlist.Count, index + addedTracks.Count));
                    addedTracks.AddRange(authoredTracks);

                    if (tracks.Count - authoredTracks.Count == 0) continue;
                    isLimitHit = true;
                    break;
                }

                if (addedTracks.Count == 1) {
                    WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.Enqueued", author, addedTracks[0].Title.RemoveNonPrintableChars()!)));
                }
                else if (addedTracks.Count > 1) {
                    WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.EnqueuedMany", author, addedTracks.Count)));
                }

                if (isLimitHit) {
                    WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.LimitExceed", author, musicResolvers.Count - currentResolverIndex,
                        Constants.MaxTracksCount)));
                }
                else if (addedTracks.Count == 0) {
                    throw new TrackNotFoundException();
                }
            }
            finally {
                _enqueueLock.Release();
                historyEntry.Remove();
            }
        }

        public virtual async Task Enqueue(List<LavalinkTrack> tracks, int position = -1) {
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

        protected override async Task<PlayerSnapshot> GetPlayerSnapshot(PlayerSnapshotParameters parameters) {
            var snapshot = await base.GetPlayerSnapshot(parameters);
            snapshot.LoopingState = LoopingState;
            snapshot.Playlist = Playlist.ToList();
            if (parameters.SavePlaylist) {
                var playlist = await ExportPlaylist(ExportPlaylistOptions.AllData);
                snapshot.StoredPlaylist = _playlistProvider.StorePlaylist(playlist, "a" + ObjectId.NewObjectId(), UserLink.Current);
            }

            return snapshot;
        }

        public override async Task ApplyStateSnapshot(PlayerStateSnapshot playerSnapshot) {
            Playlist.Clear();
            Playlist.AddRange(playerSnapshot.Playlist);
            LoopingState = playerSnapshot.LoopingState;
            if (playerSnapshot.LastTrack == null && Playlist.Count != 0) {
                await PlayAsync(Playlist.First());
            }
            await base.ApplyStateSnapshot(playerSnapshot);
            UpdateCurrentTrackIndex();
        }

        public override async Task OnTrackExceptionAsync(TrackExceptionEventArgs eventArgs) {
            WriteToQueueHistory(new EntryLocalized("Music.TrackException", eventArgs.ErrorMessage));
            await SkipAsync(1, true);
            await base.OnTrackExceptionAsync(eventArgs);
        }

        public override async Task OnTrackStuckAsync(TrackStuckEventArgs eventArgs) {
            WriteToQueueHistory(new EntryLocalized("Music.TrackStuck"));
            await SkipAsync(1, true);
            await base.OnTrackStuckAsync(eventArgs);
        }
    }

    public enum LoopingState {
        One,
        All,
        Off
    }
}