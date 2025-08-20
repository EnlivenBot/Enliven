using System.Collections.Immutable;

namespace Lavalink4NET.InactivityTracking.Trackers;

public readonly record struct PlayerTrackingState(
    PlayerTrackingStatus Status,
    ImmutableArray<PlayerTrackerInformation> Trackers);