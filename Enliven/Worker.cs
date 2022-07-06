using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Config;
using Common.Localization;
using Microsoft.Extensions.Hosting;
using NLog;

namespace Bot {
    public class Worker : BackgroundService {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly ILifetimeScope _lifetimeScope;
        public Worker(IHostApplicationLifetime hostApplicationLifetime, ILifetimeScope lifetimeScope) {
            _hostApplicationLifetime = hostApplicationLifetime;
            _lifetimeScope = lifetimeScope;
            _lifetimeScope.Disposer.AddInstanceForDisposal(this);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

        public override async Task StartAsync(CancellationToken cancellationToken) {
            var configs = PrepareInstanceConfigs();
            var enlivenBotWrappers = configs.Select(provider => new EnlivenBotWrapper(provider));
            var startTasks = enlivenBotWrappers.Select(wrapper => wrapper.StartAsync(_lifetimeScope, CancellationToken.None)).ToList();

            var whenAll = await Task.WhenAll(startTasks);
            if (whenAll.All(b => !b)) {
                // If all failed - exit
                Logger.Fatal("All bot instances failed to start. Check configs, logs and try again");
                Environment.Exit(-1);
            }
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken) {
            Logger.Fatal("Recieved application stop request. Stopping");
            await _lifetimeScope.DisposeAsync();
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose() {
            _hostApplicationLifetime.StopApplication();
            base.Dispose();
        }

        private static IEnumerable<ConfigProvider<InstanceConfig>> PrepareInstanceConfigs() {
            try {
                return ConfigProvider<InstanceConfig>.GetConfigs("Config/Instances/");
            }
            catch (Exception) when (File.Exists("Config/config.json")) {
                // Migrate old config to new folder
                Directory.CreateDirectory("Config/Instances/");
                File.Move("Config/config.json", "Config/Instances/MainBot.json");
                return PrepareInstanceConfigs();
            }
            catch (Exception) {
                var enlivenConfigProvider = new ConfigProvider<InstanceConfig>("Config/Instances/MainBot.json");
                enlivenConfigProvider.Load();
                throw new Exception($"New config file generated at {enlivenConfigProvider.FullConfigPath}. Consider check it.");
            }
        }
    }
}