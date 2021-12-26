using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Patches;
using Common;
using Common.Config;
using Common.Localization;
using Common.Utils;
using Discord;
using NLog;

namespace Bot {
    public class EnlivenBot : AsyncDisposableBase, IService {
        private readonly EnlivenShardedClient _client;
        private readonly ILogger _logger;
        private readonly IEnumerable<IService> _services;
        private readonly EnlivenConfig _config;
        private bool _isDiscordStarted;

        public EnlivenBot(ILogger logger, IEnumerable<IService> services,
                          EnlivenShardedClient discordShardedClient, EnlivenConfig config) {
            _config = config;
            config.Load();

            _services = services;
            _logger = logger;
            _client = discordShardedClient;
        }

        internal async Task StartAsync() {
            _logger.Info("Start Initialising");

            await Task.WhenAll(_services.Select(service => service.OnPreDiscordLoginInitialize()).ToArray());

            _client.Log += OnClientLog;

            await LoginAsync();

            LocalizationManager.Initialize();

            await Task.WhenAll(_services.Select(service => service.OnPreDiscordStartInitialize()).ToArray());

            await StartClient();

            await Task.WhenAll(_services.Select(service => service.OnPostDiscordStartInitialize()).ToArray());
        }

        private async Task LoginAsync() {
            _logger.Info("Start logining");
            for (int connectionTryNumber = 1; connectionTryNumber <= 5; connectionTryNumber++) {
                try {
                    await _client.LoginAsync(TokenType.Bot, _config.BotToken);
                    _logger.Info("Successefully logged in");
                    return;
                }
                catch (Exception e) {
                    _logger.Fatal(e, "Failed to login. Probably token is incorrect - {token}", _config.BotToken);
                    _logger.Info("Waiting before next attempt - {delay}s", connectionTryNumber * 10);
                    await Task.Delay(TimeSpan.FromSeconds(connectionTryNumber * 10));
                }
            }

            _logger.Fatal("Failed to login 5 times. Quiting");
            throw new Exception("Failed to login 5 times");
        }

        public async Task StartClient() {
            _logger.Info("Starting client");
            await _client.StartAsync();
            await _client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
            _isDiscordStarted = true;
        }

        public async Task OnShutdown(bool isDiscordStarted) {
            if (isDiscordStarted) {
                await _client.SetStatusAsync(UserStatus.AFK);
                await _client.SetGameAsync("Reboot...");
            }
        }

        private Task OnClientLog(LogMessage message) {
            if (message.Message != null && message.Message.StartsWith("Unknown Dispatch")) {
                return Task.CompletedTask;
            }

            _logger.Log(message.Severity, message.Exception, "{message} from {source}", message.Message!, message.Source);
            return Task.CompletedTask;
        }

        protected override async Task DisposeInternalAsync() {
            await Task.WhenAll(_services.Select(service => service.OnShutdown(_isDiscordStarted)).ToArray()).WhenEnd();
            _client.Dispose();
        }
    }
}