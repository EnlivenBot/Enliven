using System;
using System.Threading.Tasks;
using Common;
using Common.Config;
using NLog;
using SpotifyAPI.Web;

namespace Bot.Music.Spotify
{
    public class SpotifyClientResolver : IService
    {
        private GlobalConfig _config;
        private ILogger _logger;
        private Task<SpotifyClient?>? _getSpotifyInternal;

        public SpotifyClientResolver(GlobalConfig config, ILogger logger)
        {
            _logger = logger;
            _config = config;
            _config.Load();
        }
        
        public Task<SpotifyClient?> GetSpotify()
        {
            return _getSpotifyInternal ??= InitializeSpotifyInternal();
        }

        private async Task<SpotifyClient?> InitializeSpotifyInternal()
        {
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
            } catch (APIException apiException) {
                _logger.Error(apiException, "Wrong Spotify credentials. Check credentials in config file");
                return null;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Spotify auth failed due to unknown reasons. We will try again later.");
                return null;
            }
        }
        
        public Task OnPreDiscordStart() {
            return _getSpotifyInternal ??= InitializeSpotifyInternal();
        }
    }
}