using System;
using System.Net.Http;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageComponents;
using Bot.DiscordRelated.Music;
using Bot.Music.Deezer;
using Bot.Music.Spotify;
using Bot.Music.Vk;
using Bot.Utilities.Collector;
using Bot.Utilities.Logging;
using Common;
using Common.Config;
using Common.Music.Cluster;
using Common.Music.Effects;
using Common.Music.Resolvers;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.Cluster.Extensions;
using Lavalink4NET.DiscordNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VkNet;
using VkNet.AudioBypassService.Extensions;
using YandexMusicResolver;
using YandexMusicResolver.Config;
using YandexMusicResolver.Loaders;

namespace Bot;

internal static class DiHelpers
{
    public static ContainerBuilder AddEnlivenServices(this ContainerBuilder builder)
    {
        builder.RegisterType<MusicResolverService>().AsSelf().SingleInstance();
        builder.RegisterModule<BotInstanceNlogModule>();
        builder.RegisterType<EnlivenBot>().InstancePerBot();
        builder.Register(context => new EnlivenShardedClient(new DiscordSocketConfig { MessageCacheSize = 100 }))
            .AsSelf().AsImplementedInterfaces().As<DiscordShardedClient>().InstancePerBot();

        // Discord type readers
        builder.RegisterType<LoopingStateTypeReader>().As<CustomTypeReader>().SingleInstance();

        // Music resolvers
        builder.RegisterType<DeezerMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();

        builder.ConfigureOptions<SpotifyCredentials>();
        builder.RegisterType<SpotifyMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<Music.Yandex.YandexMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();

        // Providers
        builder.RegisterType<EmbedPlayerDisplayProvider>().As<IService>().AsSelf().InstancePerBot();
        builder.RegisterType<EmbedPlayerQueueDisplayProvider>().InstancePerBot();
        builder.RegisterType<EmbedPlayerEffectsDisplayProvider>().InstancePerBot();
        builder.RegisterType<EffectSourceProvider>().SingleInstance();

        // Services
        builder.RegisterType<CustomCommandService>().As<IService>().AsSelf().InstancePerBot();
        builder.RegisterType<CustomInteractionService>().As<IService>().AsSelf().InstancePerBot();
        builder.RegisterType<GlobalBehaviorsService>().As<IService>().AsSelf().InstancePerBot();
        builder.RegisterType<ScopedReliabilityService>().As<IService>().AsSelf().InstancePerBot();
        builder.RegisterType<InteractionHandlerService>().As<IService>().AsSelf().InstancePerBot();
        builder.RegisterType<CommandHandlerService>().As<IService>().AsSelf().InstancePerBot();
        builder.RegisterType<StatisticsService>().As<IStatisticsService>().AsSelf().InstancePerBot();
        builder.RegisterType<MessageComponentService>().AsSelf().InstancePerBot();
        builder.RegisterType<CollectorService>().InstancePerBot();

        return builder;
    }

    public static ContainerBuilder AddYandexResolver(this ContainerBuilder builder)
    {
        builder.Configure<YandexCredentials>();
        builder.RegisterType<YandexMusicAuthService>().As<IYandexMusicAuthService>().SingleInstance()
            .UsingConstructor(typeof(IHttpClientFactory));
        builder.RegisterType<YandexCredentialsProvider>().As<IYandexCredentialsProvider>().SingleInstance();
        builder.RegisterType<YandexMusicMainResolver>().As<IYandexMusicMainResolver>().SingleInstance()
            .UsingConstructor(typeof(IYandexCredentialsProvider), typeof(IHttpClientFactory),
                typeof(IYandexMusicPlaylistLoader), typeof(IYandexMusicTrackLoader),
                typeof(IYandexMusicDirectUrlLoader), typeof(IYandexMusicSearchResultLoader));

        return builder;
    }

    public static ContainerBuilder AddLavalink(this ContainerBuilder builder, InstanceConfig instanceConfig)
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<IEnlivenClusterAudioService, EnlivenClusterAudioService>()
            .AddSingleton<IClusterAudioService>(provider => provider.GetRequiredService<IEnlivenClusterAudioService>())
            .AddSingleton<IAudioService>(provider => provider.GetRequiredService<IEnlivenClusterAudioService>())
            .AddLavalinkCluster<DiscordClientWrapper>()
            .AddSingleton(instanceConfig)
            .ConfigureOptions<ClusterAudioServiceOptionsConfigurator>();

        builder.Populate(serviceCollection);

        return builder;
    }

    public static ContainerBuilder ConfigureOptions<T>(this ContainerBuilder builder) where T : class
    {
        builder.Register(context =>
                new OptionsWrapper<T>(context.Resolve<IConfiguration>().GetSection(typeof(T).Name).Get<T>()!))
            .As<IOptions<T>>();

        return builder;
    }

    public static ContainerBuilder Configure<T>(this ContainerBuilder builder) where T : class
    {
        builder.Register(context => context.Resolve<IConfiguration>().GetSection(typeof(T).Name).Get<T>()!);

        return builder;
    }

    public static ContainerBuilder AddVk(this ContainerBuilder builder)
    {
        builder.ConfigureOptions<VkCredentials>();
        builder.Register(c => new VkApi(new ServiceCollection().AddAudioBypass()))
            .AsImplementedInterfaces()
            .AsSelf()
            .SingleInstance();
        builder.Register(_ => new VkMusicCacheService(TimeSpan.FromDays(1), ".cache/vkmusic/", "mp3"))
            .AsSelf();
        builder.RegisterType<VkMusicSeederService>()
            .AsImplementedInterfaces()
            .AsSelf()
            .SingleInstance();
        builder.RegisterType<VkMusicResolver>()
            .AsImplementedInterfaces()
            .AsSelf()
            .SingleInstance();

        return builder;
    }
}