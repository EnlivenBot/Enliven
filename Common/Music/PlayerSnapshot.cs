using System;
using System.Collections.Generic;
using Common.Music.Players;
using Lavalink4NET.Player;

namespace Common.Music {
    public class PlayerSnapshot {
        public ulong GuildId { get; set; }
        public ulong LastVoiceChannelId { get; set; }
        
        public StoredPlaylist? StoredPlaylist { get; set; }
        

        public LavalinkTrack? LastTrack { get; set; }
        public LavalinkPlaylist? Playlist { get; set; }
        public TimeSpan TrackPosition { get; set; }
        public PlayerState PlayerState { get; set; }
        public LoopingState LoopingState { get; set; } 
        
        public List<PlayerEffectUse> Effects { get; set; }
    }
}