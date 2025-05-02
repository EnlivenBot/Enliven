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
using Lavalink4NET.Tracks;
using MessagePack;
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


    public PlaylistLavalinkPlayer(IPlayerProperties<PlaylistLavalinkPlayer, PlaylistLavalinkPlayerOptions> options)
        : base(options)
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
        await base.NotifyTrackEndedAsync(track, endReason, cancellationToken);

        if (endReason is TrackEndReason.Replaced or TrackEndReason.LoadFailed) return;

        await SkipAsync();
    }

    protected override async ValueTask NotifyTrackExceptionAsync(ITrackQueueItem track, TrackException exception,
        CancellationToken cancellationToken = new())
    {
        await base.NotifyTrackExceptionAsync(track, exception, cancellationToken);

        WriteToQueueHistory(new EntryLocalized("Music.TrackException", exception.Format()));
        var enlivenItem = track.As<IEnlivenQueueItem>() ?? CurrentItem;
        if (enlivenItem != null)
        {
            enlivenItem.PlaybackExceptionCount++;
            if (enlivenItem.PlaybackExceptionCount > 2)
            {
                if (Playlist.Count - CurrentTrackIndex > 1)
                {
                    await SkipAsync(1, true);
                }
            }
            else
            {
                var currentPosition = Position?.Position;
                if (currentPosition is not null && currentPosition.Value.TotalSeconds < 10)
                {
                    currentPosition = null;
                }

                await PlayAsync(enlivenItem, new TrackPlayProperties(currentPosition), cancellationToken);
            }
        }
    }

    protected override async ValueTask NotifyTrackStuckAsync(ITrackQueueItem track, TimeSpan threshold,
        CancellationToken cancellationToken = new())
    {
        await base.NotifyTrackStuckAsync(track, threshold, cancellationToken);
        WriteToQueueHistory(new EntryLocalized("Music.TrackStuck"));
        await SkipAsync(1, true);
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
        if (!force && LoopingState == LoopingState.One)
        {
            // TODO Log warning if current played track index doesn't exists to repeat track with LoopingState.One
            if (!Playlist.TryGetValue(CurrentTrackIndex, out var currentTrack)) return false;
            await PlayAsync(currentTrack);
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
        var byteTracks = encodedTracks.Select(track => MessagePackSerializer.Typeless.Serialize(track)).ToArray();
        var exportPlaylist = new EnlivenPlaylist { Tracks = byteTracks };
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
        if (Playlist.Count + playlist.Tracks.Length > 10000)
        {
            var historyEntry = new HistoryEntry(new EntryLocalized("MusicQueues.PlaylistLoadingLimit", 
                requester, playlist.Tracks.Length, Constants.MaxTracksCount));
            WriteToQueueHistory(historyEntry);
            return;
        }

        var trackPlaylist = new TrackPlaylist("Enliven's playlist", null);
        var encodedTracks = playlist.Tracks
            .Select(bytes => MessagePackSerializer.Typeless.Deserialize(bytes))
            .Cast<IEncodedTrack>();
        var tracks = await _musicResolverService.DecodeTracks(encodedTracks)
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
                : tracks[playlist.TrackIndex.Normalize(0, playlist.Tracks.Length - 1)];
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

    public virtual async Task ResolveAndEnqueue(IEnumerable<string> queries, TrackRequester requester, int? insertAt)
    {
        var queriesArray = queries.ToImmutableArray();
        var currentResolverIndex = 0;
        var addedTracks = new List<EnlivenQueueItem>();
        var historyEntry = new HistoryEntry(new EntryLocalized("Music.ResolvingTracks",
            // ReSharper disable once AccessToModifiedClosure
            () => requester, () => queriesArray.Length, () => currentResolverIndex, () => addedTracks.Count));
        WriteToQueueHistory(historyEntry);
        await _enqueueLock.WaitAsync();
        var resolutionScope = new LavalinkApiResolutionScope(ApiClient);
        using var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var isLimitHit = false;
            foreach (var query in queriesArray)
            {
                currentResolverIndex++;
                if (Constants.MaxTracksCount - Playlist.Tracks.Count <= 0)
                {
                    isLimitHit = true;
                    await cancellationTokenSource.CancelAsync();
                    break;
                }

                historyEntry.Update();
                var resolveResult =
                    await _musicResolverService.ResolveTracks(resolutionScope, query, cancellationTokenSource.Token);
                if (resolveResult.Exception is not null) continue;

                var playlist = resolveResult.Playlist?.Name.Pipe(name => new TrackPlaylist(name, query));
                await foreach (var track in resolveResult.Tracks.WithCancellation(cancellationTokenSource.Token))
                {
                    var queueItem = new EnlivenQueueItem(track, requester, playlist);
                    var position = Math.Min(Playlist.Count, insertAt ?? Playlist.Count + addedTracks.Count);
                    Playlist.Insert(position, queueItem);
                    addedTracks.Add(queueItem);
                    if (State == PlayerState.NotPlaying && (resolveResult.Playlist?.SelectedTrack is null
                                                            || track == resolveResult.Playlist?.SelectedTrack))
                    {
                        await PlayAsync(queueItem, cancellationToken: CancellationToken.None);
                    }

                    if (Constants.MaxTracksCount - Playlist.Tracks.Count > 0) continue;
                    isLimitHit = true;
                    await cancellationTokenSource.CancelAsync();
                    break;
                }
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
                WriteToQueueHistory(new EntryLocalized("Music.NothingFound"));
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
        snapshot.LastTrack = CurrentItem;
        snapshot.LoopingState = LoopingState;
        snapshot.Playlist = Playlist.ToList();
        await base.FillPlayerSnapshot(snapshot);
    }

    protected override ITrackQueueItem? LookupTrackQueueItem(LavalinkTrack receivedTrack, ITrackQueueItem? currentTrack,
        string? overridenTrackIdentifier)
    {
        var baseLookup = base.LookupTrackQueueItem(receivedTrack, currentTrack, overridenTrackIdentifier);
        if (baseLookup is not null) return baseLookup;

        if (receivedTrack.AdditionalInformation.TryGetValue("EnlivenCorrelationId", out var recId))
        {
            if (currentTrack?.Track?.AdditionalInformation.TryGetValue("EnlivenCorrelationId", out var curId) == true
                && recId.ToString() == curId.ToString())
                return currentTrack;

            if (Playlist.TryGetValue(CurrentTrackIndex, out var track)
                && track.Track.AdditionalInformation.TryGetValue("EnlivenCorrelationId", out var curIndexId)
                && recId.ToString() == curIndexId.ToString())
                return track;

            var matchedPlaylistTrack = Playlist.FirstOrDefault(item =>
                item.Track.AdditionalInformation.TryGetValue("EnlivenCorrelationId", out var eId)
                && recId.ToString() == eId.ToString());

            if (matchedPlaylistTrack != null) return matchedPlaylistTrack;
        }

        return Playlist.FirstOrDefault(item => item.Track.Identifier == receivedTrack.Identifier);
    }
}

public enum LoopingState
{
    One,
    All,
    Off
}