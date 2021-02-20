using System;
using System.Threading.Tasks;
using Common.Config;
using NLog;
using SpotifyAPI.Web;

namespace Bot.Music.Spotify
{
    public class SpotifyClientResolver
    {
        private EnlivenConfig _config;
        private ILogger _logger;
        private Task<SpotifyClient?>? _getSpotifyInternal;

        public SpotifyClientResolver(EnlivenConfig config, ILogger logger)
        {
            _logger = logger;
            _config = config;
            _config.Load();
        }
        
        public Task<SpotifyClient?> GetSpotify()
        {
            return _getSpotifyInternal ??= GetSpotifyInternal();
        }

        private async Task<SpotifyClient?> GetSpotifyInternal()
        {
            try
            {
                var spotifyConfig = SpotifyClientConfig.CreateDefault();

                var request = new ClientCredentialsRequest(_config.SpotifyClientID, _config.SpotifyClientSecret);
                // If credentials wrong, this \/ line will throw the exception
                await new OAuthClient(spotifyConfig).RequestToken(request);

                var actualConfig = SpotifyClientConfig
                    .CreateDefault()
                    .WithAuthenticator(new ClientCredentialsAuthenticator(_config.SpotifyClientID, _config.SpotifyClientSecret));
                return new SpotifyClient(actualConfig);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Wrong Spotify credentials. Check config file");
                return null;
            }
        }
    }
}