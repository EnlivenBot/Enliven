using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Common.Infrastructure.Tracing;
using Common.Music.Tracks;
using JetBrains.Annotations;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Microsoft.Extensions.Logging;

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member

namespace Common.Music.Players;

public class WrappedLavalinkPlayer : LavalinkPlayer
{
    private readonly Subject<PlayerState> _stateChanged = new();

    private readonly Subject<int> _volumeChanged = new();

    /// <inheritdoc />
    public WrappedLavalinkPlayer(IPlayerProperties<LavalinkPlayer, LavalinkPlayerOptions> options) : base(options)
    {
        options.Logger.LogInformation("Lavalink player created for {ChannelId} with {@InitialOptions}",
            options.VoiceChannelId, options.InitialState);
    }

    public IObservable<int> VolumeChanged => _volumeChanged.AsObservable();
    public IObservable<PlayerState> StateChanged => _stateChanged.AsObservable();

    public new IEnlivenQueueItem? CurrentItem
    {
        get
        {
            var trackQueueItem = base.CurrentItem;
            if (trackQueueItem is TrackQueueItem) Debug.Assert(trackQueueItem is TrackQueueItem);

            return (IEnlivenQueueItem?)trackQueueItem;
        }
    }

    public virtual ValueTask SetVolumeAsync(int volume, CancellationToken token = new())
    {
        using var _ = CreateActivity(nameof(SetVolumeAsync), "LavalinkPlayer changing volume to {volume}", volume);
        volume = volume.Normalize(0, 200);
        _volumeChanged.OnNext(volume);
        return base.SetVolumeAsync((float)volume / 200, token);
    }

    /// <inheritdoc />
    [Obsolete("Use SetVolumeAsync which accept int as a first parameter", true)]
    public sealed override ValueTask SetVolumeAsync(float volume, CancellationToken cancellationToken = new())
    {
        return SetVolumeAsync((int)(volume * 200), cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask PauseAsync(CancellationToken cancellationToken = new())
    {
        using var _ = CreateActivity(nameof(PauseAsync), "LavalinkPlayer pausing");

        await base.PauseAsync(cancellationToken);
        _stateChanged.OnNext(State);
    }

    public override async ValueTask ResumeAsync(CancellationToken cancellationToken = new())
    {
        using var _ = CreateActivity(nameof(ResumeAsync), "LavalinkPlayer resuming");

        await base.ResumeAsync(cancellationToken);
        _stateChanged.OnNext(State);
    }

    protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track,
        CancellationToken cancellationToken = new())
    {
        await base.NotifyTrackStartedAsync(track, cancellationToken);
        _stateChanged.OnNext(State);
    }

    protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem track, TrackEndReason endReason,
        CancellationToken cancellationToken = new())
    {
        await base.NotifyTrackEndedAsync(track, endReason, cancellationToken);
        _stateChanged.OnNext(State);
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