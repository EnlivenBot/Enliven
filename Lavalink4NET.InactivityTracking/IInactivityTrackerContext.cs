using Lavalink4NET.InactivityTracking.Trackers;
using Lavalink4NET.Players;

namespace Lavalink4NET.InactivityTracking;

public interface IInactivityTrackerContext {
    IInactivityTracker InactivityTracker { get; }

    InactivityTrackerScope CreateScope();

    InactivityTrackerEntry? GetEntry(ILavalinkPlayer player);

    InactivityTrackerEntry? GetEntry(ulong guildId);
}