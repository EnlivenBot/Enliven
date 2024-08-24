using Common.Music.Resolvers;
using MessagePack;

namespace Bot.Music.Vk;

[MessagePackObject]
public record VkTrackData(long? Id, string Url) : IEncodedTrack
{
    [Key(0)] public long? Id { get; set; } = Id;
    [Key(1)] public string Url { get; set; } = Url;
}