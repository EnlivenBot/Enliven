using System;
using System.Collections.Generic;
using Common.Music.Players;
using Lavalink4NET.Player;

namespace Common.Music {
    public class PlayerSnapshot : PlayerStateSnapshot {
        public ulong GuildId { get; set; }
        public ulong LastVoiceChannelId { get; set; }
        
        public StoredPlaylist? StoredPlaylist { get; set; }
    }
    public class PlayerStateSnapshot {
        public LavalinkTrack? LastTrack { get; set; }
        public List<LavalinkTrack> Playlist { get; set; } = new();
        public TimeSpan TrackPosition { get; set; }
        public PlayerState PlayerState { get; set; }
        public LoopingState LoopingState { get; set; }
        public List<PlayerEffectUse> Effects { get; set; } = new();
    }
}