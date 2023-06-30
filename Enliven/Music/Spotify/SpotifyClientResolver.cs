using System;
using System.Threading.Tasks;
using Common.Config;
using Microsoft.Extensions.Options;
using NLog;
using SpotifyAPI.Web;

namespace Bot.Music.Spotify;

public class SpotifyClientResolver {
    private readonly SpotifyCredentials _config;
    private Task<SpotifyClient?>? _getSpotifyInternal;
    private ILogger _logger;

    public SpotifyClientResolver(IOptions<SpotifyCredentials> options, ILogger logger) {
        _logger = logger;
        _config = options.Value;
        _ = GetSpotify();
    }

    public Task<SpotifyClient?> GetSpotify() {
        return _getSpotifyInternal ??= InitializeSpotifyInternal();
    }

    private async Task<SpotifyClient?> InitializeSpotifyInternal() {
        if (_config.SpotifyClientID == null || _config.SpotifyClientSecret == null) {
            _logger.Warn("Spotify credentials not supplied. Spotify disabled");
            return null;
        }

        try {
            var spotifyConfig = SpotifyClientConfig.CreateDefault();

            var request = new ClientCredentialsRequest(_config.SpotifyClientID, _config.SpotifyClientSecret);
            // If credentials wrong, this \/ line will throw the exception
            await new OAuthClient(spotifyConfig).RequestToken(request);

            _logger.Info("Spotify auth completed");

            var actualConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new ClientCredentialsAuthenticator(_config.SpotifyClientID, _config.SpotifyClientSecret));
            return new SpotifyClient(actualConfig);
        }
        catch (APIException apiException) {
            _logger.Error(apiException, "Wrong Spotify credentials. Check credentials in options file");
            return null;
        }
        catch (Exception e) {
            _logger.Error(e, "Spotify auth failed due to unknown reasons. We will try again later.");
            return null;
        }
    }
}