using System.Collections.Generic;
using Lavalink4NET.Players;

namespace Common.Music.Players.Options;

public record AdvancedLavalinkPlayerOptions : LavalinkPlayerOptions
{
    public IEnumerable<PlayerEffectUse>? PlayerEffects { get; init; }
}