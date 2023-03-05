using System.Net.Http;
using Autofac;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageComponents;
using Bot.DiscordRelated.MessageHistories;
using Bot.DiscordRelated.Music;
using Bot.Music.Deezer;
using Bot.Music.Spotify;
using Bot.Music.Yandex;
using Bot.Utilities.Collector;
using Bot.Utilities.Logging;
using ChatExporter;
using ChatExporter.Exporter.MessageHistories;
using Common;
using Common.Config;
using Common.Entities;
using Common.Music.Effects;
using Common.Music.Resolvers;
using Discord.WebSocket;
using Lavalink4NET.Artwork;
using NLog;
using YandexMusicResolver;
using YandexMusicResolver.Config;
using YandexMusicResolver.Loaders;

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
            builder.RegisterModule<BotInstanceNlogModule>();
            builder.RegisterType<EnlivenBot>().InstancePerBot();
            builder.Register(context => new EnlivenShardedClient(new DiscordSocketConfig { MessageCacheSize = 100 }))
                .AsSelf().AsImplementedInterfaces().As<DiscordShardedClient>().InstancePerBot();

            // Discord type readers
            builder.RegisterType<ChannelFunctionTypeReader>().As<CustomTypeReader>().SingleInstance();
            builder.RegisterType<LoopingStateTypeReader>().As<CustomTypeReader>().SingleInstance();

            // Database types
            builder.Register(context => context.GetDatabase().GetCollection<SpotifyAssociation>(@"SpotifyAssociations")).SingleInstance();
            builder.Register(context => context.GetDatabase().GetCollection<MessageHistory>(@"MessageHistory")).SingleInstance();

            // Music resolvers
            builder.RegisterType<DeezerMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<SpotifyMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SpotifyClientResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<Music.Yandex.YandexMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<SpotifyTrackEncoderUtil>().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies)
                .AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<YandexTrackEncoderUtil>().AsSelf().AsImplementedInterfaces().SingleInstance();

            // Providers
            builder.RegisterType<SpotifyAssociationProvider>().As<ISpotifyAssociationProvider>().SingleInstance();
            builder.RegisterType<MessageHistoryProvider>().SingleInstance();
            builder.RegisterType<EmbedPlayerDisplayProvider>().As<IService>().AsSelf().InstancePerBot();
            builder.RegisterType<EmbedPlayerQueueDisplayProvider>().InstancePerBot();
            builder.RegisterType<EmbedPlayerEffectsDisplayProvider>().InstancePerBot();
            builder.RegisterType<EffectSourceProvider>().SingleInstance();

            // Services
            builder.RegisterType<CustomCommandService>().As<IService>().AsSelf().InstancePerBot();
            builder.RegisterType<CustomInteractionService>().As<IService>().AsSelf().InstancePerBot();
            builder.RegisterType<MessageHistoryService>().As<IService>().AsSelf().InstancePerBot();
            builder.RegisterType<GlobalBehaviorsService>().As<IService>().AsSelf().InstancePerBot();
            builder.RegisterType<ScopedReliabilityService>().As<IService>().AsSelf().InstancePerBot();
            builder.RegisterType<InteractionHandlerService>().As<IService>().AsSelf().InstancePerBot();
            builder.RegisterType<CommandHandlerService>().As<IService>().AsSelf().InstancePerBot();
            builder.RegisterType<StatisticsService>().As<IStatisticsService>().AsSelf().InstancePerBot();
            builder.RegisterType<MessageComponentService>().AsSelf().InstancePerBot();
            builder.RegisterType<HtmlRendererService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageHistoryHtmlExporter>().InstancePerBot();
            builder.RegisterType<CollectorService>().InstancePerBot();
            builder.RegisterType<ArtworkService>().As<IArtworkService>().SingleInstance();

            // MessageHistory Printers
            builder.RegisterType<MessageHistoryPrinter>().InstancePerBot();
            builder.RegisterType<MessageHistoryPackPrinter>().InstancePerBot();

            return builder;
        }

        public static ContainerBuilder AddYandexResolver(this ContainerBuilder builder) {
            builder.Register(context => GetYandexCredentials(context.Resolve<GlobalConfig>())).SingleInstance();
            builder.RegisterType<YandexMusicAuthService>().As<IYandexMusicAuthService>().SingleInstance()
                .UsingConstructor(typeof(IHttpClientFactory));
            builder.RegisterType<YandexCredentialsProvider>().As<IYandexCredentialsProvider>().SingleInstance();
            builder.RegisterType<YandexMusicMainResolver>().As<IYandexMusicMainResolver>().SingleInstance()
                .UsingConstructor(typeof(IYandexCredentialsProvider), typeof(IHttpClientFactory),
                    typeof(IYandexMusicPlaylistLoader), typeof(IYandexMusicTrackLoader), typeof(IYandexMusicDirectUrlLoader), typeof(IYandexMusicSearchResultLoader));

            return builder;
        }

        private static YandexCredentials GetYandexCredentials(GlobalConfig globalConfig) {
            return new YandexCredentials() { Login = globalConfig.YandexLogin, Password = globalConfig.YandexPassword, Token = globalConfig.YandexToken };
        }
    }
}