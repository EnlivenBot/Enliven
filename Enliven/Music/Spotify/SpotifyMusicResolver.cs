using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Config;
using Common.Music.Resolvers;
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
    private readonly ILogger<SpotifyMusicResolver> _logger;
    private readonly SpotifyCredentials _spotifyCredentials;
    private SpotifyClient? _spotifyClient;

    public SpotifyMusicResolver(IOptions<SpotifyCredentials> options, ILogger<SpotifyMusicResolver> logger)
    {
        _logger = logger;
        _spotifyCredentials = options.Value;

        _ = InitializeSpotifyInternal();
    }

    public bool IsAvailable => _spotifyClient != null;
    public bool CanResolve(string query) => new SpotifyUrl(query).IsValid;

    public async ValueTask<MusicResolveResult> Resolve(ITrackManager cluster,
        LavalinkApiResolutionScope resolutionScope,
        string query, CancellationToken cancellationToken)
    {
        Debug.Assert(_spotifyClient is not null);
        var spotifyUrl = new SpotifyUrl(query);
        var spotifyTrackWrappers = await spotifyUrl.Resolve(_spotifyClient);
        var tracks = spotifyTrackWrappers.ToAsyncEnumerable()
            .SelectAwait(async wrapper => (await cluster.LoadTrackAsync(await wrapper.GetTrackInfo(_spotifyClient),
                TrackSearchMode.SoundCloud, resolutionScope, cancellationToken), wrapper))
            .Where(tuple => tuple.Item1 is not null)
            .Select(x => new SpotifyLavalinkTrack(x.wrapper, x.Item1!, _spotifyClient));
        return new MusicResolveResult(tracks);
    }

    public bool CanEncodeTrack(LavalinkTrack track) => false;

    public ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track) => throw new NotSupportedException();

    public bool CanDecodeTrack(IEncodedTrack track) => false;

    public ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(params IEncodedTrack[] tracks) =>
        throw new NotSupportedException();

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