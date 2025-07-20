using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Config.Emoji;
using Common.Music.Tracks;
using Discord;
using Lavalink4NET.Tracks;
using VkNet.Model;

namespace Bot.Music.Vk;

public record VkLavalinkTrack : LavalinkTrack, ITrackHasCustomSource, ITrackHasArtwork, ITrackNeedPrefetch,
    ITrackHasCustomQueueTitle {
    private readonly VkMusicSeederService _vkMusicSeederService;

    [SetsRequiredMembers]
    public VkLavalinkTrack(Audio audio, VkMusicSeederService vkMusicSeederService) {
        Audio = audio;
        _vkMusicSeederService = vkMusicSeederService;

        var trackIdentifier = $"{audio.OwnerId}_{audio.Id}";

        Author = audio.Artist;
        Duration = TimeSpan.FromSeconds(audio.Duration);
        IsSeekable = true;
        Uri = new Uri($"https://vk.com/audio{trackIdentifier}");
        Title = audio.Title;
        Identifier = trackIdentifier;
        SourceName = "http";
        ProbeInfo = "mp3";
        AdditionalInformation = new Dictionary<string, JsonElement> {
                { "EnlivenCorrelationId", JsonSerializer.SerializeToElement(Guid.NewGuid()) }
            }
            // ReSharper disable once UsageOfDefaultStructEquality
            .ToImmutableDictionary();
    }

    public Audio Audio { get; }

    /// <inheritdoc />
    public ValueTask<Uri?> GetArtwork() {
        var thumbUri = Audio.Album?.Thumb?.Photo68
            ?.Pipe(s => new Uri(s));
        return new ValueTask<Uri?>(thumbUri);
    }

    /// <inheritdoc />
    public Emote CustomSourceEmote => CommonEmoji.VkMusic;

    public Uri CustomSourceUrl => Uri!;

    /// <inheritdoc />
    public Task PrefetchTrack() {
        return _vkMusicSeederService.PrepareTrackAndGetUrl(Audio);
    }

    public override async ValueTask<LavalinkTrack> GetPlayableTrackAsync(
        CancellationToken cancellationToken = new CancellationToken()) {
        var directUrl = await _vkMusicSeederService.PrepareTrackAndGetUrl(Audio);
        return new LavalinkTrack {
            Author = Author,
            Duration = Duration,
            IsLiveStream = IsLiveStream,
            IsSeekable = IsSeekable,
            StartPosition = StartPosition,
            Uri = directUrl,
            Title = Title,
            Identifier = directUrl.ToString(),
            SourceName = "http",
            ProbeInfo = "mp3",
            AdditionalInformation = AdditionalInformation
        };
    }

    public string GetQueueTitle() {
        return $"{Audio.Artist} - {Audio.Title}";
    }
}