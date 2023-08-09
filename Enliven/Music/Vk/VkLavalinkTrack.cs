using System;
using System.Threading.Tasks;
using Common;
using Common.Config.Emoji;
using Common.Music.Tracks;
using Discord;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;
using VkNet.Model;

namespace Bot.Music.Vk;

public class VkLavalinkTrack : LavalinkTrack, ITrackHasCustomSource, ITrackHasArtwork, ITrackNeedPrefetch {
    private readonly Audio _audio;
    private readonly VkMusicSeederService _vkMusicSeederService;
    /// <inheritdoc />
    private VkLavalinkTrack(Audio audio, VkMusicSeederService vkMusicSeederService, string identifier, LavalinkTrackInfo trackInformation) : base(identifier, trackInformation) {
        _audio = audio;
        _vkMusicSeederService = vkMusicSeederService;
    }

    /// <inheritdoc />
    public ValueTask<Uri?> GetArtwork() {
        var thumbUri = _audio.Album?.Thumb?.Photo68
            ?.Pipe(s => new Uri(s));
        return new ValueTask<Uri?>(thumbUri);
    }

    /// <inheritdoc />
    public Emote CustomSourceEmote => CommonEmoji.VkMusic;

    /// <inheritdoc />
    public Uri CustomSourceUrl => Uri!;

    /// <inheritdoc />
    public Task PrefetchTrack() {
        return _vkMusicSeederService.PrepareTrackAndGetUrl(_audio);
    }
    public static VkLavalinkTrack CreateInstance(Audio audio, VkMusicSeederService musicSeederService) {
        var trackIdentifier = $"{audio.OwnerId}_{audio.Id}";
        var lavalinkTrackInfo = new LavalinkTrackInfo() {
            Author = audio.Artist, Duration = TimeSpan.FromSeconds(audio.Duration), IsLiveStream = false, IsSeekable = true,
            Position = TimeSpan.Zero, Uri = new Uri($"https://vk.com/audio{trackIdentifier}"), Title = audio.Title,
            TrackIdentifier = trackIdentifier, SourceName = "http", ProbeInfo = "mp3"
        };
        return new VkLavalinkTrack(audio, musicSeederService, TrackEncoder.Encode(lavalinkTrackInfo), lavalinkTrackInfo);
    }

    /// <inheritdoc />
    public override async ValueTask<LavalinkTrack> GetPlayableTrack() {
        var directUrl = await _vkMusicSeederService.PrepareTrackAndGetUrl(_audio);
        var lavalinkTrackInfo = new LavalinkTrackInfo() {
            Author = Author, Duration = Duration, IsLiveStream = IsLiveStream, IsSeekable = IsSeekable,
            Position = Position, Uri = directUrl, Title = Title, TrackIdentifier = directUrl.ToString(),
            SourceName = "http", ProbeInfo = "mp3"
        };
        return lavalinkTrackInfo.CreateTrack();
    }
}