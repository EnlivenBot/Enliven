using System.Collections.Generic;
using Common.Music;
using Common.Music.Tracks;

namespace Bot.Music.Players.Options;

public record PlaylistLavalinkPlayerOptions : AdvancedLavalinkPlayerOptions {
    public LoopingState? LoopingState { get; set; }
    public IEnumerable<IEnlivenQueueItem>? Playlist { get; set; }
}