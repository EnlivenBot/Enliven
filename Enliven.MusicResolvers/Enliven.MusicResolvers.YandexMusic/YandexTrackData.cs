using Enliven.MusicResolvers.Base;
using MessagePack;
using YandexMusicResolver.Ids;

namespace Enliven.MusicResolver.YandexMusic;

[MessagePackObject]
public record YandexTrackData(YandexId Id) : IEncodedTrack {
    [Key(0)] public YandexId Id { get; set; } = Id;
}