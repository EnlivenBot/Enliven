using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Config.Emoji;
using Common.Music.Tracks;
using Discord;
using Lavalink4NET.Tracks;
using SpotifyAPI.Web;

namespace Bot.Music.Spotify;

public record SpotifyLavalinkTrack : LavalinkTrack, ITrackHasArtwork, ITrackHasCustomSource
{
    private Uri? _artwork;
    private bool _isArtworkResolved;
    private SpotifyClient _spotifyClient;

    [SetsRequiredMembers]
    public SpotifyLavalinkTrack(SpotifyTrackWrapper relatedSpotifyTrackWrapper, LavalinkTrack track,
        SpotifyClient spotifyClient)
        : base(track)
    {
        CustomSourceUrl = new Uri($"https://open.spotify.com/track/{relatedSpotifyTrackWrapper.Id}");
        RelatedSpotifyTrackWrapper = relatedSpotifyTrackWrapper;
        _spotifyClient = spotifyClient;
    }

    public SpotifyTrackWrapper RelatedSpotifyTrackWrapper { get; }

    public async ValueTask<Uri?> GetArtwork()
    {
        if (_isArtworkResolved)
        {
            return _artwork;
        }

        var imageUrl = await RelatedSpotifyTrackWrapper.GetFullTrack(_spotifyClient)
            .PipeAsync(track => track.Album.Images.FirstOrDefault())
            .PipeAsync(image => image?.Url);
        _artwork = imageUrl?.Pipe(s => new Uri(s));
        _isArtworkResolved = true;
        return _artwork;
    }

    /// <inheritdoc />
    public Emote CustomSourceEmote => CommonEmoji.Spotify;

    /// <inheritdoc />
    public Uri CustomSourceUrl { get; }
}