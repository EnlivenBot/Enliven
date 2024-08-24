using Common.Music.Resolvers;
using MessagePack;
using YandexMusicResolver.Ids;

namespace Bot.Music.Yandex;

[MessagePackObject]
public record YandexTrackData(YandexId Id) : IEncodedTrack
{
    [Key(0)] public YandexId Id { get; set; } = Id;
}