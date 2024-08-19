using MessagePack;

namespace Common.Music.Resolvers.Lavalink;

[MessagePackObject]
public record LavalinkTrackData(string StringTrackData) : IEncodedTrack
{
    [Key(0)] public string StringTrackData { get; set; } = StringTrackData;
}