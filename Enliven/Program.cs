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
using NLog;

namespace Bot {
    internal static class Program {
        private static IContainer Container { get; set; } = null!;
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static async Task Main(string[] args) {
#if !DEBUG
            InstallErrorHandlers();
#endif

            LocalizationManager.Initialize();

            var containerBuilder = new ContainerBuilder()
                .AddGlobalConfig()
                .AddEnlivenServices()
                .AddCommonServices();
            Container = containerBuilder.Build();

            var configs = PrepareInstanceConfigs();
            var enlivenBotWrappers = configs.Select(provider => new EnlivenBotWrapper(provider));
            var startTasks = enlivenBotWrappers.Select(wrapper => wrapper.StartAsync(Container, CancellationToken.None)).ToList();

            var whenAll = await Task.WhenAll(startTasks);
            if (whenAll.All(b => !b)) {
                // If all failed - exit
                Logger.Fatal("All bot instances failed to start. Check configs, logs and try again");
                Environment.Exit(-1);
            }

            AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainOnProcessExit;
            await Task.Delay(-1);
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

        private static void OnCurrentDomainOnProcessExit(object? o, EventArgs eventArgs) {
            Logger.Info("Application shutdown requested");
            Container.DisposeAsync().AsTask().Wait();
            Logger.Info("Application shutdowned");
            LogManager.Shutdown();
            Environment.Exit(0);
        }

        // ReSharper disable once UnusedMember.Local
        private static void InstallErrorHandlers() {
            var logger = LogManager.GetLogger("Global");
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                logger.Fatal(args.ExceptionObject as Exception, "Global uncaught exception");
            TaskScheduler.UnobservedTaskException += (sender, args) =>
                logger.Fatal(args.Exception?.Flatten(), "Global uncaught task exception");
        }
    }
}