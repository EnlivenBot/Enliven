using Discord;

namespace Common.Music.Tracks;

public readonly record struct TrackRequester
{
    private readonly object _value;

    public TrackRequester(string value)
    {
        _value = value;
    }

    public TrackRequester(IUser? value)
    {
        _value = value;
    }

    public override string ToString()
    {
        if (_value is string s)
        {
            return s;
        }

        if (_value is IUser user)
        {
            return user.Mention;
        }

        return "UNKNOWN";
    }
}