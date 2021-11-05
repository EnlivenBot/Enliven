using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Patches;
using Common;
using Common.Config;
using Common.Localization;
using Discord;
using NLog;

namespace Bot {
    public class EnlivenBot : IService {
        public static EnlivenShardedClient Client = null!;

        // ReSharper disable once InconsistentNaming
        private readonly ILogger logger;
        private IEnumerable<IService> _services;
        private IEnumerable<IPatch> _patches;
        private EnlivenConfig _config;
        private bool _isDiscordStarted;

        public EnlivenBot(ILogger logger, IEnumerable<IService> services, IEnumerable<IPatch> patches,
                          EnlivenShardedClient discordShardedClient, EnlivenConfig config) {
            _config = config;
            config.Load();

            _patches = patches;
            _services = services;
            this.logger = logger;
            Client = discordShardedClient;
        }

        internal async Task StartAsync() {
            logger.Info("Start Initialising");

            AppDomain.CurrentDomain.ProcessExit += async (sender, eventArgs) =>
                await Task.WhenAll(_services.Select(service => service.OnShutdown(_isDiscordStarted)).ToArray());

            await Task.WhenAll(_patches.Select(patch => patch.Apply()).ToArray());
            await Task.WhenAll(_services.Select(service => service.OnPreDiscordLoginInitialize()).ToArray());

            Client.Log += OnClientLog;

            await LoginAsync();

            LocalizationManager.Initialize();

            await Task.WhenAll(_services.Select(service => service.OnPreDiscordStartInitialize()).ToArray());

            await StartClient();

            await Task.WhenAll(_services.Select(service => service.OnPostDiscordStartInitialize()).ToArray());
        }

        private async Task LoginAsync() {
            logger.Info("Start logining");
            for (int connectionTryNumber = 0; connectionTryNumber < 5; connectionTryNumber++) {
                try {
                    await Client.LoginAsync(TokenType.Bot, _config.BotToken);
                    logger.Info("Successefully logged in");
                    return;
                }
                catch (Exception e) {
                    logger.Fatal(e, "Failed to login. Probably token is incorrect - {token}", _config.BotToken);
                    logger.Info("Waiting before next attempt - {delay}s", connectionTryNumber * 10);
                    await Task.Delay(TimeSpan.FromSeconds(connectionTryNumber++ * 10));
                }
            }

            logger.Fatal("Failed to login 5 times. Quiting");
            throw new Exception("Failed to login 5 times");
        }

        public async Task StartClient() {
            logger.Info("Starting client");
            await Client.StartAsync();
            await Client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
            _isDiscordStarted = true;
        }

        public async Task OnShutdown(bool isDiscordStarted) {
            if (isDiscordStarted) {
                await Client.SetStatusAsync(UserStatus.AFK);
                await Client.SetGameAsync("Reboot...");
            }
        }

        private Task OnClientLog(LogMessage message) {
            if (message.Message != null && message.Message.StartsWith("Unknown Dispatch")) {
                return Task.CompletedTask;
            }

            logger.Log(message.Severity, message.Exception, "{message} from {source}", message.Message!, message.Source);
            return Task.CompletedTask;
        }
    }
}