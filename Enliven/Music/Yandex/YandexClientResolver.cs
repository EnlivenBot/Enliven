using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using Common.Config;
using Common.Utils;
using NLog;
using YandexMusicResolver;

namespace Bot.Music.Yandex {
    public class YandexClientResolver : IConfigDependent {
        private readonly EnlivenConfig _config;
        private readonly ILogger _logger;
        private readonly YandexMusicMainResolver _resolver;
        private readonly SingleTask _retryAuthTask;

        private bool _isAuthFailed;
        private bool _isWrongCredentials;
        private bool _isInitialized;

        public YandexClientResolver(EnlivenConfig config, ILogger logger) {
            _logger = logger;
            _config = config;
            _retryAuthTask = new SingleTask(async () => {
                try {
                    await _config.AuthorizeAsync(false);
                    _isAuthFailed = false;
                }
                catch (AuthenticationException) {
                    _isWrongCredentials = true;
                    _logger.Error("Yandex Music auth failed - wrong credentials. Yandex Music tracks cut to 30 seconds");
                }
                catch (Exception) {
                    _isAuthFailed = true;
                }
            }) {BetweenExecutionsDelay = TimeSpan.FromMinutes(10), CanBeDirty = false};
            _resolver = new YandexMusicMainResolver(_config);
        }

        public async Task<YandexMusicMainResolver> GetClient() {
            await Initialize();
            if (_isAuthFailed && !_isWrongCredentials) {
                await _retryAuthTask.Execute();
            }

            return _resolver;
        }


        public async Task Initialize() {
            if (_isInitialized) return;
            _isInitialized = true;
            try {
                await _config.AuthorizeAsync(false);
                _logger.Info("Yandex Music auth completed");
            }
            catch (AuthenticationException) {
                _logger.Error("Yandex Music auth failed - wrong credentials. Yandex Music tracks cut to 30 seconds");
                _isWrongCredentials = true;
            }
            catch (Exception e) {
                _logger.Error(e, "Yandex Music auth failed due to unknown reasons. We will try again later.");
                _isAuthFailed = true;
            }
        }

        public Task OnConfigLoaded()
        {
            return Initialize();
        }
    }
}