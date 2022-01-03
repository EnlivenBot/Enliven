using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extras.NLog;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.MessageComponents;
using Bot.DiscordRelated.MessageHistories;
using Bot.DiscordRelated.Music;
using Bot.Music.Spotify;
using Bot.Music.Yandex;
using Bot.Patches;
using Bot.Utilities.Collector;
using ChatExporter;
using ChatExporter.Exporter.MessageHistories;
using Common;
using Common.Config;
using Common.Entities;
using Common.Localization;
using Common.Music.Controller;
using Common.Music.Effects;
using Common.Music.Resolvers;
using Discord.WebSocket;
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

            var containerBuilder = new ContainerBuilder();
            SetupMainConfig(containerBuilder);
            ConfigureServices(containerBuilder);
            Startup.ConfigureServices(containerBuilder);
            Container = containerBuilder.Build();
            await Task.WhenAll(Container.Resolve<IEnumerable<IPatch>>().Select(patch => patch.Apply()).ToArray());
            AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainOnProcessExit;

            var configs = PrepareInstanceConfigs();
            var enlivenBotWrappers = configs.Select(provider => new EnlivenBotWrapper(provider));
            var startTasks = enlivenBotWrappers.Select(wrapper => wrapper.StartAsync(Container, CancellationToken.None)).ToList();

            var whenAll = await Task.WhenAll(startTasks);
            if (whenAll.All(b => !b)) {
                // If all failed - exit
                Logger.Fatal("All bot instances failed to start. Check configs, logs and try again");
                Environment.Exit(-1);
            }

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

        private static void SetupMainConfig(ContainerBuilder builder) {
            var configProvider = new ConfigProvider<GlobalConfig>("Config/MainConfig.json");
            // Hook old config if possible
            if (!configProvider.IsConfigExists()) {
                var oldConfigProvider = new ConfigProvider<GlobalConfig>("Config/config.json");
                if (oldConfigProvider.IsConfigExists()) {
                    oldConfigProvider.Load();
                    oldConfigProvider.ConfigPath = "Config/MainConfig.json";
                    oldConfigProvider.Save();
                    configProvider = oldConfigProvider;
                }
                else {
                    Logger.Warn("Main config created from scratch. Consider check it!");
                }
            }

            builder.Register(context => configProvider.Load())
                .AsSelf().AsImplementedInterfaces()
                .SingleInstance();
        }

        private static void OnCurrentDomainOnProcessExit(object? o, EventArgs eventArgs) {
            Logger.Info("Application shutdown requested");
            Container.DisposeAsync().AsTask().Wait();
            Logger.Info("Application shutdowned");
            LogManager.Shutdown();
            Environment.Exit(0);
        }

        public static void ConfigureServices(ContainerBuilder builder)
        {
            builder.RegisterType<MusicResolverService>().AsSelf().SingleInstance();
            builder.RegisterType<MusicController>().As<IMusicController>().InstancePerLifetimeScope();
            builder.RegisterModule<NLogModule>();
            builder.RegisterType<EnlivenBot>().InstancePerLifetimeScope();
            builder.Register(context => new EnlivenShardedClient(new DiscordSocketConfig {MessageCacheSize = 100}))
                .AsSelf().AsImplementedInterfaces().As<DiscordShardedClient>().InstancePerLifetimeScope();

            builder.Register(context => context.Resolve<EnlivenConfig>().LavalinkNodes).InstancePerLifetimeScope();

            // Discord type readers
            builder.RegisterType<ChannelFunctionTypeReader>().As<CustomTypeReader>().SingleInstance();
            builder.RegisterType<LoopingStateTypeReader>().As<CustomTypeReader>().SingleInstance();

            // Database types
            builder.Register(context => context.GetDatabase().GetCollection<SpotifyAssociation>(@"SpotifyAssociations")).SingleInstance();
            builder.Register(context => context.GetDatabase().GetCollection<MessageHistory>(@"MessageHistory")).SingleInstance();

            // Music resolvers
            builder.RegisterType<SpotifyMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SpotifyClientResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            
            builder.RegisterType<YandexClientResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<Music.Yandex.YandexMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            
            builder.RegisterType<SpotifyTrackEncoder>().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies)
                .AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<YandexTrackEncoder>().AsSelf().AsImplementedInterfaces().SingleInstance();

            // Providers
            builder.RegisterType<SpotifyAssociationProvider>().As<ISpotifyAssociationProvider>().SingleInstance();
            builder.RegisterType<MessageHistoryProvider>().SingleInstance();
            builder.RegisterType<EmbedPlayerDisplayProvider>().InstancePerLifetimeScope();
            builder.RegisterType<EmbedPlayerQueueDisplayProvider>().InstancePerLifetimeScope();
            builder.RegisterType<EmbedPlayerEffectsDisplayProvider>().InstancePerLifetimeScope();
            builder.RegisterType<EffectSourceProvider>().SingleInstance();

            // Services
            builder.RegisterType<CustomCommandService>().As<IService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<MessageHistoryService>().As<IService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<GlobalBehaviorsService>().As<IService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<ScopedReliabilityService>().As<IService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<CommandHandlerService>().As<IService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<StatisticsService>().As<IStatisticsService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<MessageComponentService>().As<MessageComponentService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<HtmlRendererService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageHistoryHtmlExporter>().InstancePerLifetimeScope();
            builder.RegisterType<CollectorService>().InstancePerLifetimeScope();
            
            // MessageHistory Printers
            builder.RegisterType<MessageHistoryPrinter>().InstancePerLifetimeScope();
            builder.RegisterType<MessageHistoryPackPrinter>().InstancePerLifetimeScope();
        }

        // ReSharper disable once UnusedMember.Local
        private static void InstallErrorHandlers()
        {
            var logger = LogManager.GetLogger("Global");
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                logger.Fatal(args.ExceptionObject as Exception, "Global uncaught exception");
            TaskScheduler.UnobservedTaskException += (sender, args) =>
                logger.Fatal(args.Exception?.Flatten(), "Global uncaught task exception");
        }
    }
}