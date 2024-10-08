﻿using Common.Music.Players.Options;
using Lavalink4NET.Players;

namespace Common.Music.Players;

// Defines the final player class for the end use
public class EnlivenLavalinkPlayer : PlaylistLavalinkPlayer
{
    public EnlivenLavalinkPlayer(
        IPlayerProperties<PlaylistLavalinkPlayer, PlaylistLavalinkPlayerOptions> options)
        : base(options)
    {
    }
}