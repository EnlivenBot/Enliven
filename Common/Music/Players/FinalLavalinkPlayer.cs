using Common.Config;
using Common.Music.Controller;
using Common.Music.Encoders;

namespace Common.Music.Players {
    // Defines the final player class for the end use
    public class FinalLavalinkPlayer : PlaylistLavalinkPlayer {
        public FinalLavalinkPlayer(IMusicController musicController, IGuildConfigProvider guildConfigProvider, IPlaylistProvider playlistProvider,
                                   TrackEncoder trackEncoder)
            : base(musicController, guildConfigProvider, playlistProvider, trackEncoder) { }
    }
}