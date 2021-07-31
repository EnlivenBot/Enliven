using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extras.NLog;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Logging;
using Bot.DiscordRelated.MessageComponents;
using Bot.DiscordRelated.Music;
using Bot.Music.Spotify;
using Bot.Music.Yandex;
using Bot.Patches;
using ChatExporter;
using Common;
using Common.Config;
using Common.Entities;
using Common.Localization;
using Common.Music.Controller;
using Common.Music.Resolvers;
using Discord;
using Discord.WebSocket;
using NLog;

namespace Bot {
    internal static class Program {
        private static async Task Main(string[] args) {
            #if !DEBUG
            InstallErrorHandlers();
            #endif

            var containerBuilder = new ContainerBuilder();
            ConfigureServices(containerBuilder);
            Startup.ConfigureServices(containerBuilder);
            Container = containerBuilder.Build();

            using (var scope = Container.BeginLifetimeScope()) {
                var bot = scope.Resolve<EnlivenBot>();
                await bot.Run();
            }

            Console.WriteLine("Execution end");
        }

        private static IContainer Container { get; set; } = null!;

        public static void ConfigureServices(ContainerBuilder builder)
        {
            builder.AddEnlivenConfig();
            builder.RegisterType<MusicResolverService>().AsSelf().SingleInstance();
            builder.RegisterType<MusicController>().As<IMusicController>().SingleInstance();
            builder.RegisterType<ReliabilityService>().AsSelf();
            builder.RegisterModule<NLogModule>();
            builder.RegisterType<EnlivenBot>().SingleInstance();
            builder.Register(context => new EnlivenShardedClient(new DiscordSocketConfig {MessageCacheSize = 100}))
                .AsSelf().As<DiscordShardedClient>().SingleInstance();

            builder.Register(context => context.Resolve<EnlivenConfig>().LavalinkNodes);

            // Discord type readers
            builder.RegisterType<ChannelFunctionTypeReader>().As<CustomTypeReader>();
            builder.RegisterType<LoopingStateTypeReader>().As<CustomTypeReader>();
            builder.RegisterType<BassBoostModeTypeReader>().As<CustomTypeReader>();

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

            // Services
            builder.RegisterType<CustomCommandService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageHistoryService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<GlobalBehaviorsService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<ReliabilityService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<CommandHandlerService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<StatisticsService>().As<IStatisticsService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageComponentService>().As<MessageComponentService>().AsSelf().SingleInstance();
            builder.RegisterType<HtmlRendererService>().As<IService>().AsSelf().SingleInstance();
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