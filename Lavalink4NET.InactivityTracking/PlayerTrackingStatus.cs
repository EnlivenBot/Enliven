namespace Lavalink4NET.InactivityTracking;

/// <summary>
///     The tracking states for players.
/// </summary>
public enum PlayerTrackingStatus : byte {
    /// <summary>
    ///     The player is not tracked and is active.
    /// </summary>
    NotTracked,

    /// <summary>
    ///     The player is tracked for inactivity, but the stop delay was not exceeded.
    /// </summary>
    Tracked,
}