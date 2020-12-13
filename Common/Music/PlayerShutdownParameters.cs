using System;
using Common.Music.Players;
using Lavalink4NET.Player;

namespace Common.Music {
    public class PlayerShutdownParameters {
        #region Parameters

        public bool NeedSave { get; set; } = true;
        public bool ShutdownDisplays { get; set; } = true;

        #endregion

        #region Storage

        public StoredPlaylist? StoredPlaylist { get; set; }
        
        public ulong GuildId { get; set; }
        public ulong LastVoiceChannelId { get; set; }
        public LavalinkTrack? LastTrack { get; set; }
        public LavalinkPlaylist? Playlist { get; set; }
        public TimeSpan TrackPosition { get; set; }
        public PlayerState PlayerState { get; set; }
        public LoopingState LoopingState { get; set; } 

        #endregion
    }
}