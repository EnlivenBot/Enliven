using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VkNet.Abstractions;
using VkNet.Model;
using VkNet.Utils;

namespace Bot.Music.Vk;

public class VkUrl
{
    private static readonly Regex VkTrackRegex = new(@"https://vk\.com/audio(-?\d*_\d*)", RegexOptions.Compiled);

    private static readonly Regex VkAlbumRegex =
        new(@"https://vk\.com/music/album/(-?\d*)_(\d*)_(\S*)", RegexOptions.Compiled);

    private static readonly Regex VkUserRegex = new(@"https://vk\.com/audios(-?\d*)", RegexOptions.Compiled);

    public VkUrl(string request)
    {
        Request = request;
        if (!request.StartsWith("https://vk.com"))
        {
            Type = AudioUrlType.Unknown;
            Id = request;
            return;
        }

        var trackMatch = VkTrackRegex.Match(request);
        if (trackMatch.Success)
        {
            Id = trackMatch.Groups[1].Value;
            Type = AudioUrlType.Track;
            return;
        }

        var albumMatch = VkAlbumRegex.Match(request);
        if (albumMatch.Success)
        {
            Id = albumMatch.Groups[1].Value;
            PlaylistId = albumMatch.Groups[2].Value;
            AccessKey = albumMatch.Groups[3].Value;
            Type = AudioUrlType.Album;
            return;
        }

        var userMatch = VkUserRegex.Match(request);
        if (userMatch.Success)
        {
            Id = albumMatch.Groups[1].Value;
            Type = AudioUrlType.User;
            return;
        }

        Id = request;
        Type = AudioUrlType.Unknown;
    }

    public VkUrl(string id, AudioUrlType type, string? playlistId, string? accessKey)
    {
        Id = id;
        Request = id;
        Type = type;
        PlaylistId = playlistId;
        AccessKey = accessKey;
    }

    public string Id { get; private set; }
    public string? PlaylistId { get; private set; }
    public string? AccessKey { get; private set; }
    public string Request { get; private set; }
    public AudioUrlType Type { get; private set; }
    public bool IsValid => Type != AudioUrlType.Unknown;

    public async Task<IReadOnlyList<Audio>> Resolve(IVkApi vkApi)
    {
        return (Type switch
        {
            AudioUrlType.Album => await ResolveAlbum(vkApi),
            AudioUrlType.Playlist => throw new NotSupportedException(),
            AudioUrlType.Track => await ResolveTrack(vkApi),
            AudioUrlType.Artist => throw new NotSupportedException(),
            AudioUrlType.User => await ResolveUser(vkApi),
            AudioUrlType.Unknown => throw new NotSupportedException(),
            _ => throw new ArgumentOutOfRangeException()
        }).ToImmutableArray();
    }

    private async Task<IEnumerable<Audio>> ResolveUser(IVkApi vkApi)
    {
        return await vkApi.CallAsync<VkCollection<Audio>>("audio.get", new VkParameters()
        {
            {
                "owner_id", long.Parse(Id)
            }
        });
    }

    private async Task<IEnumerable<Audio>> ResolveAlbum(IVkApi vkApi)
    {
        return await vkApi.CallAsync<VkCollection<Audio>>("audio.get", new VkParameters()
        {
            {
                "owner_id", long.Parse(Id)
            },
            {
                "playlist_id", long.Parse(PlaylistId!)
            },
            {
                "access_key", AccessKey
            }
        });
    }

    private async Task<IEnumerable<Audio>> ResolveTrack(IVkApi vkApi)
    {
        return await vkApi.Audio.GetByIdAsync(new[] { Id });
    }
}