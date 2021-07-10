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
    public class EnlivenBot {
        public static EnlivenShardedClient Client = null!;
        
        // ReSharper disable once InconsistentNaming
        private readonly ILogger logger;
        private IEnumerable<IService> _services;
        private IEnumerable<IPatch> _patches;
        private EnlivenConfig _config;

        public EnlivenBot(ILogger logger, IEnumerable<IService> services, IEnumerable<IPatch> patches,
                       EnlivenShardedClient discordShardedClient, EnlivenConfig config)
        {
            _config = config;
            config.Load();

            _patches = patches;
            _services = services;
            this.logger = logger;
            Client = discordShardedClient;
        }

        internal async Task Run()
        {
            logger.Info("Start Initialising");

            await Task.WhenAll(_patches.Select(patch => patch.Apply()).ToArray());
            await Task.WhenAll(_services.Select(service => service.OnPreDiscordLoginInitialize()).ToArray());

            Client.Log += OnClientLog;

            logger.Info("Start logining");
            var connectDelay = 30;
            while (true)
            {
                try
                {
                    await Client.LoginAsync(TokenType.Bot, _config.BotToken);
                    logger.Info("Successefully logged in");
                    break;
                }
                catch (Exception e)
                {
                    logger.Fatal(e, "Failed to login. Probably token is incorrect - {token}", _config.BotToken);
                    logger.Info("Waiting before next attempt - {delay}s", connectDelay);
                    await Task.Delay(TimeSpan.FromSeconds(connectDelay));
                    connectDelay += 10;
                }
            }

            LocalizationManager.Initialize();
            
            await Task.WhenAll(_services.Select(service => service.OnPreDiscordStartInitialize()).ToArray());

            await StartClient();

            await Task.WhenAll(_services.Select(service => service.OnPostDiscordStartInitialize()).ToArray());

            AppDomain.CurrentDomain.ProcessExit += async (sender, eventArgs) =>
            {
                await Client.SetStatusAsync(UserStatus.AFK);
                await Client.SetGameAsync("Reboot...");
            };

            await Task.Delay(-1);
        }
        
        public async Task StartClient()
        {
            logger.Info("Starting client");
            await Client.StartAsync();
            await Client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
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