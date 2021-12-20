using System;
using System.Collections.Generic;
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
using Common.Music.Controller;
using Common.Music.Effects;
using Common.Music.Resolvers;
using Discord.WebSocket;
using NLog;

namespace Bot {
    internal static class Program {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static async Task Main(string[] args) {
            #if !DEBUG
            InstallErrorHandlers();
            #endif

            var containerBuilder = new ContainerBuilder();
            ConfigureServices(containerBuilder);
            Startup.ConfigureServices(containerBuilder);
            Container = containerBuilder.Build();
            await Task.WhenAll(Container.Resolve<IEnumerable<IPatch>>().Select(patch => patch.Apply()).ToArray());

            await using var scope = Container.BeginLifetimeScope();
            AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainOnProcessExit;
            var bot = scope.Resolve<EnlivenBot>();
            await bot.StartAsync();
            await Task.Delay(-1);
        }

        private static void OnCurrentDomainOnProcessExit(object? o, EventArgs eventArgs) {
            Logger.Info("Application shutdown requested");
            Container.DisposeAsync().AsTask().Wait();
            Logger.Info("Application shutdowned");
            LogManager.Shutdown();
            Environment.Exit(0);
        }

        private static IContainer Container { get; set; } = null!;

        public static void ConfigureServices(ContainerBuilder builder)
        {
            builder.AddEnlivenConfig();
            builder.RegisterType<MusicResolverService>().AsSelf().SingleInstance();
            builder.RegisterType<MusicController>().As<IMusicController>().SingleInstance();
            builder.RegisterModule<NLogModule>();
            builder.RegisterType<EnlivenBot>().SingleInstance();
            builder.Register(context => new EnlivenShardedClient(new DiscordSocketConfig {MessageCacheSize = 100}))
                .AsSelf().AsImplementedInterfaces().As<DiscordShardedClient>().SingleInstance();

            builder.Register(context => context.Resolve<EnlivenConfig>().LavalinkNodes);

            // Discord type readers
            builder.RegisterType<ChannelFunctionTypeReader>().As<CustomTypeReader>();
            builder.RegisterType<LoopingStateTypeReader>().As<CustomTypeReader>();

            // Database types
            builder.Register(context => context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                .GetCollection<SpotifyAssociation>(@"SpotifyAssociations")).SingleInstance();
            builder.Register(context => context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                .GetCollection<MessageHistory>(@"MessageHistory")).SingleInstance();

            // Music resolvers
            builder.RegisterType<SpotifyMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SpotifyClientResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            
            builder.RegisterType<YandexClientResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<Music.Yandex.YandexMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            
            builder.RegisterType<SpotifyTrackEncoder>().AsSelf().AsImplementedInterfaces().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies).SingleInstance();
            builder.RegisterType<YandexTrackEncoder>().AsSelf().AsImplementedInterfaces().SingleInstance();

            // Providers
            builder.RegisterType<SpotifyAssociationProvider>().As<ISpotifyAssociationProvider>().SingleInstance();
            builder.RegisterType<MessageHistoryProvider>().SingleInstance();
            builder.RegisterType<EmbedPlayerDisplayProvider>().SingleInstance();
            builder.RegisterType<EmbedPlayerQueueDisplayProvider>().SingleInstance();
            builder.RegisterType<EffectSourceProvider>().SingleInstance();
            builder.RegisterType<EmbedPlayerEffectsDisplayProvider>().SingleInstance();

            // Services
            builder.RegisterType<CustomCommandService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageHistoryService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<GlobalBehaviorsService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<ScopedReliabilityService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<CommandHandlerService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<StatisticsService>().As<IStatisticsService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageComponentService>().As<MessageComponentService>().AsSelf().SingleInstance();
            builder.RegisterType<HtmlRendererService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageHistoryHtmlExporter>().SingleInstance();
            builder.RegisterType<CollectorService>().SingleInstance();
            
            // MessageHistory Printers
            builder.RegisterType<MessageHistoryPrinter>().SingleInstance();
            builder.RegisterType<MessageHistoryPackPrinter>().SingleInstance();
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