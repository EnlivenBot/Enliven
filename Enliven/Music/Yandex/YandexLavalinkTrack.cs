using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Config.Emoji;
using Common.Music.Tracks;
using Discord;
using Lavalink4NET.Tracks;
using YandexMusicResolver.AudioItems;
using YandexMusicResolver.Loaders;

namespace Bot.Music.Yandex;

public record YandexLavalinkTrack : LavalinkTrack, ITrackHasArtwork, ITrackHasCustomSource
{
    private static readonly HttpClient HttpClient = new();

    private static readonly Dictionary<long, string> UrlCache = new();
    private IYandexMusicDirectUrlLoader _directUrlLoader;

    [SetsRequiredMembers]
    public YandexLavalinkTrack(YandexMusicTrack relatedYandexTrack, IYandexMusicDirectUrlLoader directUrlLoader)
    {
        RelatedYandexTrack = relatedYandexTrack;
        _directUrlLoader = directUrlLoader;
        Uri = relatedYandexTrack.Uri!.Pipe(s => new Uri(s));
        Title = relatedYandexTrack.Title;
        Author = relatedYandexTrack.Author;
        Duration = relatedYandexTrack.Length;
        IsSeekable = true;
        Identifier = relatedYandexTrack.Id.ToString();
        SourceName = "http";
        ProbeInfo = "mp3";
    }

    public YandexMusicTrack RelatedYandexTrack { get; }

    public ValueTask<Uri?> GetArtwork()
    {
        var artworkUri = RelatedYandexTrack.ArtworkUrl?.Pipe(s => new Uri(s));
        return ValueTask.FromResult(artworkUri);
    }

    /// <inheritdoc />
    public Emote CustomSourceEmote => CommonEmoji.YandexMusic;

    public Uri CustomSourceUrl => Uri!;

    public override async ValueTask<LavalinkTrack> GetPlayableTrackAsync(
        CancellationToken cancellationToken = new CancellationToken())
    {
        var directUrl = await GetDirectUrl(RelatedYandexTrack.Id);
        return new LavalinkTrack
        {
            Author = Author,
            Duration = Duration,
            IsLiveStream = IsLiveStream,
            IsSeekable = IsSeekable,
            StartPosition = StartPosition,
            Uri = new Uri(directUrl),
            Title = Title,
            Identifier = Identifier,
            SourceName = "http",
            ProbeInfo = "mp3"
        };
    }

    private async Task<string> GetDirectUrl(long id)
    {
        if (UrlCache.TryGetValue(id, out var url))
        {
            var isUrlAccessibleResponse = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            if (isUrlAccessibleResponse.IsSuccessStatusCode)
                return url;
        }

        var directUrl = await _directUrlLoader.GetDirectUrl(id);
        UrlCache[id] = directUrl;
        return directUrl;
    }
}