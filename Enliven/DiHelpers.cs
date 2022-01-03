using Autofac;
using Autofac.Extras.NLog;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.MessageComponents;
using Bot.DiscordRelated.MessageHistories;
using Bot.DiscordRelated.Music;
using Bot.Music.Spotify;
using Bot.Music.Yandex;
using Bot.Utilities.Collector;
using ChatExporter;
using ChatExporter.Exporter.MessageHistories;
using Common;
using Common.Config;
using Common.Entities;
using Common.Music.Effects;
using Common.Music.Resolvers;
using Discord.WebSocket;
using NLog;

namespace Bot {
    internal static class DiHelpers {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public static ContainerBuilder AddGlobalConfig(this ContainerBuilder builder) {
            var configProvider = new ConfigProvider<GlobalConfig>("Config/GlobalConfig.json");
            // Hook old config if possible
            if (!configProvider.IsConfigExists()) {
                var oldConfigProvider = new ConfigProvider<GlobalConfig>("Config/config.json");
                if (oldConfigProvider.IsConfigExists()) {
                    oldConfigProvider.Load();
                    oldConfigProvider.ConfigPath = "Config/GlobalConfig.json";
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

            return builder;
        }

        public static ContainerBuilder AddEnlivenServices(this ContainerBuilder builder) {
            builder.RegisterType<MusicResolverService>().AsSelf().SingleInstance();
            builder.RegisterModule<NLogModule>();
            builder.RegisterType<EnlivenBot>().InstancePerLifetimeScope();
            builder.Register(context => new EnlivenShardedClient(new DiscordSocketConfig { MessageCacheSize = 100 }))
                .AsSelf().AsImplementedInterfaces().As<DiscordShardedClient>().InstancePerLifetimeScope();

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

            return builder;
        }
    }
}