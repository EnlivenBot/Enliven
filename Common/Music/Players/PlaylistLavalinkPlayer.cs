using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Players.Options;
using Common.Music.Resolvers;
using Common.Music.Tracks;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Music.Players;

public class PlaylistLavalinkPlayer : AdvancedLavalinkPlayer
{
    private static readonly IEntry StartPlayingFailedEntry = new EntryLocalized("Music.StartPlayingFailed");
    private readonly ISubject<int> _currentTrackIndexChanged = new Subject<int>();

    private readonly SemaphoreSlim _enqueueLock = new(1);

    private readonly ISubject<LoopingState> _loopingStateChanged = new Subject<LoopingState>();
    private readonly MusicResolverService _musicResolverService;
    private int _currentTrackIndex;
    private LoopingState _loopingState = LoopingState.Off;


    public PlaylistLavalinkPlayer(
        IPlayerProperties<PlaylistLavalinkPlayer, PlaylistLavalinkPlayerOptions> options) : base(options)
    {
        _musicResolverService = ServiceScope.ServiceProvider.GetRequiredService<MusicResolverService>();
        Playlist.Changed.Subscribe(playlist => UpdateCurrentTrackIndex());

        var properties = options.Options.Value;
        if (properties.LoopingState is not null)
        {
            _loopingState = properties.LoopingState.Value;
        }

        if (properties.Playlist is not null)
        {
            Playlist.AddRange(properties.Playlist);
            UpdateCurrentTrackIndex();
        }
    }

    public LoopingState LoopingState
    {
        get => _loopingState;
        set
        {
            _loopingState = value;
            _loopingStateChanged.OnNext(value);
        }
    }

    public IObservable<LoopingState> LoopingStateChanged => _loopingStateChanged;

    public LavalinkPlaylist Playlist { get; } = new();
    public IObservable<int> CurrentTrackIndexChanged => _currentTrackIndexChanged;

    public int CurrentTrackIndex
    {
        get => _currentTrackIndex;
        private set
        {
            if (_currentTrackIndex == value) return;
            _currentTrackIndex = value;
            _currentTrackIndexChanged.OnNext(value);
        }
    }

    protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem track, TrackEndReason endReason,
        CancellationToken cancellationToken = new())
    {
        if (endReason == TrackEndReason.Replaced) return;

        // var oldTrackIndex = CurrentTrackIndex;

        if (endReason == TrackEndReason.LoadFailed)
        {
            // if (LoadFailedId == CurrentTrack?.Identifier)
            // {
            if (Playlist.Count - CurrentTrackIndex > 1)
            {
                await PauseAsync(cancellationToken);
            }
            else
            {
                await SkipAsync();
            }

            // Playlist.RemoveAt(oldTrackIndex);
            // LoadFailedRemoves++;
            // }
            // else
            // {
            // LoadFailedId = CurrentTrack?.Identifier;
            await PlayAsync(CurrentTrack!, new TrackPlayProperties(Position?.Position), cancellationToken);
            // }
            // }
            // else
            // {
            // LoadFailedRemoves = 0;
        }

        // if (LoadFailedRemoves > 2)
        // {
        //     try
        //     {
        //         var cluster = await MusicController.ClusterTask;
        //         var currentNode = cluster.GetServingNode(GuildId);
        //         var newNode = cluster.Nodes
        //             .Where(node => node.IsConnected)
        //             .Where(node => node != currentNode)
        //             .RandomOrDefault();
        //         if (newNode != null)
        //         {
        //             await currentNode.MovePlayerAsync(this, newNode);
        //             WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.NodeChanged", "SYSTEM",
        //                 newNode.Label ?? "")));
        //         }
        //     }
        //     finally
        //     {
        //         LoadFailedRemoves = 0;
        //     }
        // }

        await SkipAsync();
    }


    public override async ValueTask PlayAsync(IEnlivenQueueItem trackQueueItem,
        TrackPlayProperties trackPlayProperties = default, CancellationToken token = default)
    {
        EnsureNotDestroyed();
        if (!Playlist.Contains(trackQueueItem))
        {
            Playlist.Add(trackQueueItem);
        }

        await base.PlayAsync(trackQueueItem, trackPlayProperties, token);

        UpdateCurrentTrackIndex();
        if (Playlist.TryGetValue(CurrentTrackIndex + 1, out var nextTrack) &&
            nextTrack.Track is ITrackNeedPrefetch needPrefetchTrack) _ = needPrefetchTrack.PrefetchTrack();
    }

    public virtual async Task<bool> SkipAsync(int count = 1, bool force = false)
    {
        EnsureNotDestroyed();
        if (!force && LoopingState == LoopingState.One && CurrentTrack != null)
        {
            await PlayAsync(CurrentTrack);
            return true;
        }

        if (Playlist.IsEmpty)
            return false;

        CurrentTrackIndex += count;
        if ((force || LoopingState == LoopingState.All) && CurrentTrackIndex > Playlist.Count - 1)
            CurrentTrackIndex = 0;
        if (force && CurrentTrackIndex < 0) CurrentTrackIndex = Playlist.Count - 1;

        if (!Playlist.TryGetValue(CurrentTrackIndex, out var track)) return false;
        await PlayAsync(track!);
        return true;
    }

    public virtual async Task<EnlivenPlaylist> ExportPlaylist(ExportPlaylistOptions options)
    {
        var encodedTracks = await _musicResolverService.EncodeTracks(Playlist);
        var exportPlaylist = new EnlivenPlaylist { Tracks = encodedTracks };
        if (options != ExportPlaylistOptions.IgnoreTrackIndex)
        {
            exportPlaylist.TrackIndex = CurrentTrackIndex.Normalize(0, Playlist.Count - 1);
        }

        if (options == ExportPlaylistOptions.AllData)
        {
            exportPlaylist.TrackPosition = Position?.Position;
        }

        return exportPlaylist;
    }

    public virtual async Task ImportPlaylist(EnlivenPlaylist playlist, ImportPlaylistOptions options,
        TrackRequester requester)
    {
        if (Playlist.Count + playlist.Tracks.Count > 10000)
        {
            WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.PlaylistLoadingLimit", requester,
                playlist.Tracks.Count,
                Constants.MaxTracksCount)));
            return;
        }

        var trackPlaylist = new TrackPlaylist("Enliven's playlist", null);
        var tracks = await _musicResolverService.DecodeTracks(playlist.Tracks)
            .PipeAsync(list => list.Select(track => new EnlivenQueueItem(track, requester, trackPlaylist)))
            .PipeAsync(items => items.ToImmutableArray());
        if (options == ImportPlaylistOptions.Replace)
        {
            try
            {
                await StopAsync();
                WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.ImportPlayerStop")));
            }
            catch (Exception)
            {
                // ignored
            }

            if (!Playlist.IsEmpty)
            {
                Playlist.Clear();
                WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.ClearPlaylist", requester)));
            }
        }

        Playlist.AddRange(tracks);
        WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.AddTracks", requester, tracks.Length)));

        if (options != ImportPlaylistOptions.JustAdd)
        {
            var item = playlist.TrackIndex == -1
                ? tracks.First()
                : tracks[playlist.TrackIndex.Normalize(0, playlist.Tracks.Count - 1)];
            var position = playlist.TrackPosition;
            if (position != null && position.Value > item.Track.Duration)
            {
                position = TimeSpan.Zero;
            }

            await PlayAsync(item, new TrackPlayProperties(position));
            WriteToQueueHistory(new EntryLocalized("MusicQueues.Jumped", requester, CurrentTrackIndex + 1,
                CurrentTrack!.Title.RemoveNonPrintableChars().SafeSubstring(100, "...")!));
        }
        else if (State == PlayerState.NotPlaying)
        {
            await PlayAsync(Playlist[0]);
        }
    }

    protected void UpdateCurrentTrackIndex()
    {
        if (CurrentItem == null)
            return;
        if (Playlist.TryGetValue(CurrentTrackIndex, out var currentPlaylistItem)
            && CurrentItem.Identifier == currentPlaylistItem.Identifier)
            return;
        CurrentTrackIndex = Playlist.IndexOfWithFallback(CurrentItem);
    }

    public virtual async Task ResolveAndEnqueue(IEnumerable<string> queries, TrackRequester requester, int index = -1)
    {
        var queriesArray = queries.ToImmutableArray();
        var currentResolverIndex = 0;
        var addedTracks = new List<EnlivenQueueItem>();
        var historyEntry = new HistoryEntry(new EntryLocalized("Music.ResolvingTracks",
            () => requester, () => queriesArray.Length, () => currentResolverIndex, () => addedTracks.Count));
        WriteToQueueHistory(historyEntry);
        await _enqueueLock.WaitAsync();
        var resolutionScope = new LavalinkApiResolutionScope(ApiClient);

        try
        {
            var isLimitHit = false;
            foreach (var query in queriesArray)
            {
                currentResolverIndex++;
                var availableNumberOfTracks = Constants.MaxTracksCount - Playlist.Tracks.Count;
                if (availableNumberOfTracks <= 0)
                {
                    isLimitHit = true;
                    break;
                }

                historyEntry.Update();
                var resolveResult = await _musicResolverService.ResolveTracks(resolutionScope, query);
                if (!resolveResult.HasMatches) continue;

                var playlist = resolveResult.Playlist?.Name.Pipe(name => new TrackPlaylist(name, query));
                var enlivenQueueItems = resolveResult.Tracks
                    .Select(track => new EnlivenQueueItem(track, requester, playlist))
                    .ToImmutableArray();


                var position = index == -1 ? index : Math.Min(Playlist.Count, index + addedTracks.Count);
                var selectedTrackInPlaylist = resolveResult.GetSelectedTrackInPlaylist(enlivenQueueItems);

                await Enqueue(enlivenQueueItems, position, selectedTrackInPlaylist);
                addedTracks.AddRange(enlivenQueueItems);

                if (resolveResult.Tracks.Length - resolveResult.Tracks.Length == 0) continue;
                isLimitHit = true;
                break;
            }

            if (addedTracks.Count == 1)
            {
                WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.Enqueued", requester,
                    addedTracks[0].Track.Title.RemoveNonPrintableChars())));
            }
            else if (addedTracks.Count > 1)
            {
                WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.EnqueuedMany", requester,
                    addedTracks.Count)));
            }

            if (isLimitHit)
            {
                WriteToQueueHistory(new HistoryEntry(new EntryLocalized("MusicQueues.LimitExceed", requester,
                    queriesArray.Length - currentResolverIndex, Constants.MaxTracksCount)));
            }
            else if (addedTracks.Count == 0)
            {
                throw new TrackNotFoundException();
            }
        }
        finally
        {
            _enqueueLock.Release();
            historyEntry.Remove();
        }
    }

    public virtual async Task Enqueue(IReadOnlyCollection<IEnlivenQueueItem> tracks, int position = -1,
        IEnlivenQueueItem? selectedTrackInPlaylist = null)
    {
        if (tracks.Count == 0)
            return;

        if (position == -1)
            Playlist.AddRange(tracks);
        else
            Playlist.InsertRange(position, tracks);

        if (State == PlayerState.NotPlaying)
        {
            await PlayAsync(selectedTrackInPlaylist ?? tracks.First());
        }
    }

    protected override async ValueTask FillPlayerSnapshot(PlayerSnapshot snapshot)
    {
        snapshot.LoopingState = LoopingState;
        snapshot.Playlist = Playlist.ToList();
        await base.FillPlayerSnapshot(snapshot);
    }

    protected override async ValueTask NotifyTrackExceptionAsync(ITrackQueueItem track, TrackException exception,
        CancellationToken cancellationToken = new())
    {
        WriteToQueueHistory(new EntryLocalized("Music.TrackException",
            exception.Message ?? exception.Cause ?? "UNKNOWN"));
        await SkipAsync(1, true);
    }

    protected override async ValueTask NotifyTrackStuckAsync(ITrackQueueItem track, TimeSpan threshold,
        CancellationToken cancellationToken = new())
    {
        WriteToQueueHistory(new EntryLocalized("Music.TrackStuck"));
        await SkipAsync(1, true);
    }
}

public enum LoopingState
{
    One,
    All,
    Off
}