using System;
using System.Collections.Generic;
using Common.Music.Tracks;
using Lavalink4NET.Players;

namespace Common.Music;

public class PlayerSnapshot {
    public ulong GuildId { get; set; }
    public ulong LastVoiceChannelId { get; set; }
    public IEnlivenQueueItem? LastTrack { get; set; }
    public List<IEnlivenQueueItem> Playlist { get; set; } = new();
    public TimeSpan? TrackPosition { get; set; }
    public PlayerState PlayerState { get; set; }
    public LoopingState LoopingState { get; set; }
    public List<PlayerEffectUse> Effects { get; set; } = new();
    public float Volume { get; set; }
}