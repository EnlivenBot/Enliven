using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Common.Music.Tracks;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Payloads.Events;

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member

namespace Common.Music.Players;

public class WrappedLavalinkPlayer : LavalinkPlayer
{
    private readonly Subject<PlayerState> _stateChanged = new();

    private readonly Subject<int> _volumeChanged = new();

    /// <inheritdoc />
    public WrappedLavalinkPlayer(IPlayerProperties<LavalinkPlayer, LavalinkPlayerOptions> options) : base(options)
    {
    }

    public IObservable<int> VolumeChanged => _volumeChanged.AsObservable();
    public IObservable<PlayerState> StateChanged => _stateChanged.AsObservable();

    public new IEnlivenQueueItem? CurrentItem => (IEnlivenQueueItem?)base.CurrentItem;

    public virtual ValueTask SetVolumeAsync(int volume, CancellationToken token = new())
    {
        volume = volume.Normalize(0, 200);
        _volumeChanged.OnNext(volume);
        return base.SetVolumeAsync((float)volume / 200, token);
    }

    /// <inheritdoc />
    [Obsolete("Use SetVolumeAsync which accept int as a first parameter")]
    public sealed override ValueTask SetVolumeAsync(float volume, CancellationToken cancellationToken = new())
    {
        return SetVolumeAsync((int)(volume * 200), cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask PauseAsync(CancellationToken cancellationToken = new())
    {
        await base.PauseAsync(cancellationToken);
        _stateChanged.OnNext(State);
    }

    public override async ValueTask ResumeAsync(CancellationToken cancellationToken = new())
    {
        await base.ResumeAsync(cancellationToken);
        _stateChanged.OnNext(State);
    }

    protected override ValueTask NotifyTrackStartedAsync(ITrackQueueItem track,
        CancellationToken cancellationToken = new())
    {
        _stateChanged.OnNext(State);
        return base.NotifyTrackStartedAsync(track, cancellationToken);
    }

    protected override ValueTask NotifyTrackEndedAsync(ITrackQueueItem track, TrackEndReason endReason,
        CancellationToken cancellationToken = new())
    {
        _stateChanged.OnNext(State);
        return base.NotifyTrackEndedAsync(track, endReason, cancellationToken);
    }

    public virtual ValueTask PlayAsync(IEnlivenQueueItem trackQueueItem, TrackPlayProperties properties = new(),
        CancellationToken cancellationToken = new())
    {
        return base.PlayAsync(trackQueueItem, properties, cancellationToken);
    }

    public sealed override ValueTask PlayAsync(ITrackQueueItem trackQueueItem, TrackPlayProperties properties = new(),
        CancellationToken cancellationToken = new())
    {
        return base.PlayAsync(trackQueueItem, properties, cancellationToken);
    }
}