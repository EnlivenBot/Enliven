using System.Collections.Generic;
using Common.Music;
using Lavalink4NET.Players;

namespace Bot.Music.Players.Options;

public record AdvancedLavalinkPlayerOptions : LavalinkPlayerOptions {
    public IEnumerable<PlayerEffectUse>? PlayerEffects { get; init; }
}