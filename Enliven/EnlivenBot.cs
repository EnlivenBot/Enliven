using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly InstanceConfig _config;
        private bool _isDiscordStarted;

        public EnlivenBot(ILogger logger, IEnumerable<IService> services,
                          EnlivenShardedClient discordShardedClient, InstanceConfig config) {
            _config = config;
            config.Load();

            _services = services;
            _logger = logger;
            _client = discordShardedClient;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default) {
            await StartAsync();
            cancellationToken.Register(Dispose);
            try { await Disposed.ToTask(CancellationToken.None); }
            catch (Exception) {
                // ignored
            }
        }

        internal async Task StartAsync() {
            _logger.Info("Start Initialising");
            await IService.ProcessEventAsync(_services, ServiceEventType.PreDiscordLogin);
            _client.Log += OnClientLog;

            await LoginAsync();
            await IService.ProcessEventAsync(_services, ServiceEventType.PreDiscordStart);

            _logger.Info("Starting client");
            await _client.StartAsync();
            _isDiscordStarted = true;
            await IService.ProcessEventAsync(_services, ServiceEventType.PostDiscordStart);

            _ = _client.Ready.ContinueWith(async _ => await IService.ProcessEventAsync(_services, ServiceEventType.DiscordReady));
        }

        private async Task LoginAsync() {
            _logger.Info("Start logining");
            for (var connectionTryNumber = 1; connectionTryNumber <= 5; connectionTryNumber++) {
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

        public async Task OnShutdown(bool isDiscordStarted) {
            if (isDiscordStarted) {
                await _client.SetStatusAsync(UserStatus.AFK);
                await _client.SetGameAsync("Reboot...");
            }
        }

        public async Task OnPostDiscordStart() {
            await _client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
        }

        private Task OnClientLog(LogMessage message) {
            if (message.Message != null && message.Message.StartsWith("Unknown Dispatch")) {
                return Task.CompletedTask;
            }

            _logger.Log(message.Severity, message.Exception, "{message} from {source}", message.Message!, message.Source);
            return Task.CompletedTask;
        }

        protected override async Task DisposeInternalAsync() {
            await IService.ProcessEventAsync(_services, _isDiscordStarted ? ServiceEventType.ShutdownStarted : ServiceEventType.ShutdownNotStarted);
            _client.Dispose();
        }
    }
}