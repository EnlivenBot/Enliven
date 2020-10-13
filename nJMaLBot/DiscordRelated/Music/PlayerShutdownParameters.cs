using System;
using Bot.Music;
using Discord;
using Lavalink4NET.Player;

namespace Bot.DiscordRelated.Music {
    public class PlayerShutdownParameters {
        #region Parameters

        public bool NeedSave { get; set; } = true;
        public bool LeaveMessageUnchanged { get; set; }
        public bool AddResumeToMessage { get; set; } = true;

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