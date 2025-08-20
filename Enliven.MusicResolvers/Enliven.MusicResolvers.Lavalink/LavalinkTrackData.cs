using Enliven.MusicResolvers.Base;
using MessagePack;

namespace Enliven.MusicResolvers.Lavalink;

[MessagePackObject]
public record LavalinkTrackData(string StringTrackData) : IEncodedTrack {
    [Key(0)] public string StringTrackData { get; set; } = StringTrackData;
}