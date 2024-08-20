using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Config;
using Common.Music.Resolvers;
using Common.Music.Resolvers.Lavalink;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

#pragma warning disable 1998

#pragma warning disable 8604
#pragma warning disable 8602

namespace Bot.Music.Spotify;

public class SpotifyMusicResolver : IMusicResolver
{
    private readonly LavalinkMusicResolver _lavalinkMusicResolver;
    private readonly ILogger<SpotifyMusicResolver> _logger;
    private readonly SpotifyCredentials _spotifyCredentials;
    private SpotifyClient? _spotifyClient;

    public SpotifyMusicResolver(LavalinkMusicResolver lavalinkMusicResolver,
        IOptions<SpotifyCredentials> options, ILogger<SpotifyMusicResolver> logger)
    {
        _lavalinkMusicResolver = lavalinkMusicResolver;
        _logger = logger;
        _spotifyCredentials = options.Value;

        _ = InitializeSpotifyInternal();
    }

    public bool IsAvailable => _spotifyClient != null;
    public bool CanResolve(string query) => new SpotifyUrl(query).IsValid;

    public async ValueTask<TrackLoadResult> Resolve(ITrackManager cluster, LavalinkApiResolutionScope resolutionScope,
        string query)
    {
        var spotifyClient = _spotifyClient!;
        var spotifyUrl = new SpotifyUrl(query);
        var spotifyTrackWrappers = await spotifyUrl.Resolve(spotifyClient);
        var spotifyLavalinkTracks =
            await ResolveInternal(cluster, resolutionScope, spotifyTrackWrappers, spotifyClient);

        return TrackLoadResult.CreateSearch(spotifyLavalinkTracks);
    }

    public bool CanEncodeTrack(LavalinkTrack track) => false;

    public ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track) => throw new NotSupportedException();

    public bool CanDecodeTrack(IEncodedTrack track) => false;

    public ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(params IEncodedTrack[] tracks) =>
        throw new NotSupportedException();

    private async Task<ImmutableArray<LavalinkTrack>> ResolveInternal(ITrackManager cluster,
        LavalinkApiResolutionScope resolutionScope,
        IReadOnlyCollection<SpotifyTrackWrapper> spotifyTrackWrappers, SpotifyClient spotifyClient)
    {
        var lavalinkTracks = await spotifyTrackWrappers
            .Select(async wrapper => (
                await cluster.LoadTrackAsync(await wrapper.GetTrackInfo(spotifyClient),
                    TrackSearchMode.SoundCloud, resolutionScope), wrapper))
            .PipeEveryAsync(x =>
                x.Item1 is not null ? new SpotifyLavalinkTrack(x.wrapper, x.Item1, _spotifyClient) : null)
            .WhenAll();

        var spotifyLavalinkTracks = lavalinkTracks
            .Where(track => track is not null)
            .OfType<LavalinkTrack>()
            .ToImmutableArray();
        return spotifyLavalinkTracks;
    }

    private async Task InitializeSpotifyInternal()
    {
        if (_spotifyCredentials.SpotifyClientID == null || _spotifyCredentials.SpotifyClientSecret == null)
        {
            _logger.LogWarning("Spotify credentials not supplied. Spotify disabled");
        }

        try
        {
            var spotifyConfig = SpotifyClientConfig.CreateDefault();

            var request = new ClientCredentialsRequest(_spotifyCredentials.SpotifyClientID,
                _spotifyCredentials.SpotifyClientSecret);
            // If credentials wrong, this \/ line will throw the exception
            await new OAuthClient(spotifyConfig).RequestToken(request);

            _logger.LogInformation("Spotify auth completed");

            var actualConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new ClientCredentialsAuthenticator(_spotifyCredentials.SpotifyClientID,
                    _spotifyCredentials.SpotifyClientSecret));
            _spotifyClient = new SpotifyClient(actualConfig);
        }
        catch (APIException apiException)
        {
            _logger.LogError(apiException, "Wrong Spotify credentials. Check credentials in options file");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Spotify auth failed due to unknown reasons. We will try again later");
        }
    }
}