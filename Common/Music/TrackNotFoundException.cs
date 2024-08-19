using System;

namespace Common.Music;

public class TrackNotFoundException : Exception
{
    public TrackNotFoundException(bool allowFallback = true)
    {
        AllowFallback = allowFallback;
    }

    public bool AllowFallback { get; private set; }
}