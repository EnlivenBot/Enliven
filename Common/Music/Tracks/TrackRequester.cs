using Discord;

namespace Common.Music.Tracks;

public readonly record struct TrackRequester
{
    private readonly object? _value;

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
        return ToString(true);
    }

    public string ToString(bool useMention)
    {
        return _value switch
        {
            string s => s,
            IUser user when useMention => user.Mention,
            IUser user => user.Username,
            _ => "UNKNOWN"
        };
    }
}