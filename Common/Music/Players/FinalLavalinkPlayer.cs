using Common.Config;
using Common.Music.Controller;

namespace Common.Music.Players {
    // Defines the final player class for the end use
    public class FinalLavalinkPlayer : PlaylistLavalinkPlayer {
        public FinalLavalinkPlayer(IMusicController musicController, IGuildConfigProvider guildConfigProvider, IPlaylistProvider playlistProvider)
            : base(musicController, guildConfigProvider, playlistProvider) { }
    }
}