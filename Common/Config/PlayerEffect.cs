using System;
using System.Collections.Generic;
using Lavalink4NET.Filters;
using LiteDB;

namespace Common.Config;

public partial class PlayerEffect : PlayerEffectBase
{
    [Obsolete("Constructor for database")]
    public PlayerEffect() : base(null!, null)
    {
    }

    public PlayerEffect(PlayerEffectBase playerEffectBase, UserLink user, string? sourceName = null)
        : this(user, playerEffectBase.DisplayName, sourceName ?? playerEffectBase.DisplayName,
            playerEffectBase.CurrentFilters)
    {
    }

    public PlayerEffect(UserLink user, string displayName, IDictionary<string, IFilterOptions>? effects = null)
        : this(user, displayName, displayName, effects)
    {
    }

    public PlayerEffect(UserLink user, string displayName, string sourceName,
        IDictionary<string, IFilterOptions>? effects = null)
        : base(displayName, effects)
    {
        User = user;
        DisplayName = displayName;
        SourceName = sourceName;
    }

    [BsonId] public string Id { get; set; } = ObjectId.NewObjectId().ToString();

    [BsonIgnore] public string SourceName { get; set; } = null!;

    public UserLink User { get; set; } = null!;
}

public partial class PlayerEffect
{
    public static PlayerEffect Effect8D { get; } = new(UserLink.Current, "8D")
    {
        Rotation = new RotationFilterOptions() { Frequency = 0.125f }
    };

    public static PlayerEffect EffectNightcore { get; } = new(UserLink.Current, "Nightcore")
    {
        Timescale = new TimescaleFilterOptions() { Rate = 1.2f, Speed = 1.2f }
    };

    public static PlayerEffect EffectBassboost { get; } = new(UserLink.Current, "Bassboost")
    {
        Equalizer = new EqualizerFilterOptions(new Equalizer()
        {
            Band0 = 0.65f,
            Band1 = 0.85f,
            Band2 = 0.45f,
            Band3 = 0.20f,
            Band4 = 0.10f,
            Band5 = 0.05f,
        })
    };

    public static PlayerEffect EffectMono { get; } = new(UserLink.Current, "Mono")
    {
        ChannelMix = new ChannelMixFilterOptions()
        {
            LeftToLeft = 0.5f,
            LeftToRight = 0.5f,
            RightToLeft = 0.5f,
            RightToRight = 0.5f
        }
    };
}