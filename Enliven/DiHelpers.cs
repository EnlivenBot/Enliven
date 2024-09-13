using System;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageComponents;
using Bot.DiscordRelated.Music;
using Bot.Music.Vk;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Music;
using Common.Music.Cluster;
using Common.Music.Resolvers;
using Common.Music.Resolvers.Lavalink;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.Cluster.Extensions;
using Lavalink4NET.Cluster.LoadBalancing.Strategies;
using Lavalink4NET.DiscordNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VkNet;
using VkNet.Abstractions;
using VkNet.AudioBypassService.Extensions;
using YandexMusicResolver;
using YandexMusicResolver.Config;

namespace Bot;

internal static class DiHelpers
{
    public static IServiceCollection AddPerBotServices(this IServiceCollection services)
    {
        // Providers
        services.AddSingleton<EmbedPlayerDisplayProvider>();
        services.AddSingleton<IService>(s => s.GetRequiredService<EmbedPlayerDisplayProvider>());
        services.AddSingleton<EmbedPlayerQueueDisplayProvider>();
        services.AddSingleton<EmbedPlayerEffectsDisplayProvider>();

        // Services
        services.AddSingleton<CustomCommandService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<CustomCommandService>());
        services.AddSingleton<CustomInteractionService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<CustomInteractionService>());
        services.AddSingleton<GlobalBehaviorsService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<GlobalBehaviorsService>());
        services.AddSingleton<ScopedReliabilityService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<ScopedReliabilityService>());
        services.AddSingleton<InteractionHandlerService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<InteractionHandlerService>());
        services.AddSingleton<CommandHandlerService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<CommandHandlerService>());
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<MessageComponentService>();
        services.AddSingleton<CollectorService>();

        return services;
    }

    public static IServiceCollection AddYandex(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNamedOptions<YandexCredentials>(configuration);
        services.AddYandexMusicResolver();
        services.AddSingleton<IMusicResolver, Music.Yandex.YandexMusicResolver>();

        return services;
    }

    public static IServiceCollection AddVk(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNamedOptions<VkCredentials>(configuration);
        services.AddSingleton<IVkApi>(_ =>
            new VkApi(new ServiceCollection().AddAudioBypass()));
        services.AddSingleton<VkMusicCacheService>(_ =>
            new VkMusicCacheService(TimeSpan.FromDays(1), ".cache/vkmusic/", "mp3"));
        services.AddSingleton<VkMusicSeederService, VkMusicSeederService>();
        services.AddSingleton<IEndpointProvider, VkMusicSeederService>(
            s => s.GetRequiredService<VkMusicSeederService>());
        services.AddSingleton<IMusicResolver, VkMusicResolver>();

        return services;
    }

    public static IServiceCollection AddLavalink(this IServiceCollection builder)
    {
        builder
            .AddSingleton<INodeBalancingStrategy, EnlivenLavalinkBalancingStrategy>()
            .AddSingleton<IEnlivenClusterAudioService, EnlivenClusterAudioService>()
            .AddSingleton<IClusterAudioService>(provider => provider.GetRequiredService<IEnlivenClusterAudioService>())
            .AddSingleton<IAudioService>(provider => provider.GetRequiredService<IEnlivenClusterAudioService>())
            .AddLavalinkCluster<DiscordClientWrapper>()
            .ConfigureOptions<ClusterAudioServiceOptionsConfigurator>()
            .AddSingleton<IMusicResolver, LavalinkMusicResolver>()
            .AddSingleton<MusicResolverService>();

        return builder;
    }

    public static IServiceCollection ConfigureNamedOptions<T>(this IServiceCollection services,
        IConfiguration configuration) where T : class
    {
        return services.Configure<T>(configuration.GetSection(typeof(T).Name));
    }
}