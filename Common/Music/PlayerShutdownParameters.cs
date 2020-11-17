using System;
using Discord;
using Lavalink4NET.Player;

namespace Common.Music {
    public class PlayerShutdownParameters {
        #region Parameters

        public bool NeedSave { get; set; } = true;
        public bool ShutdownDisplays { get; set; } = true;
        public bool CanBeResumed { get; set; } = true;

        #endregion

        #region Storage

        public IUserMessage? LastControlMessage { get; set; }
        public StoredPlaylist? StoredPlaylist { get; set; }
        
        public ulong LastVoiceChannelId { get; set; }
        public LavalinkTrack? LastTrack { get; set; }
        public LavalinkPlaylist? Playlist { get; set; }
        public TimeSpan TrackPosition { get; set; }
        public PlayerState PlayerState { get; set; }

        #endregion
    }
}