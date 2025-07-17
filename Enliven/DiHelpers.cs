using System;
using System.Collections.Immutable;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.Interactions.Handlers;
using Bot.DiscordRelated.Music;
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
using Lavalink4NET.InactivityTracking;
using Lavalink4NET.InactivityTracking.Extensions;
using Lavalink4NET.InactivityTracking.Trackers.Idle;
using Lavalink4NET.InactivityTracking.Trackers.Users;
using Lavalink4NET.Players;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YandexMusicResolver;
using YandexMusicResolver.Config;

namespace Bot;

internal static class DiHelpers {
    public static IServiceCollection AddPerBotServices(this IServiceCollection services) {
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
        services.AddSingleton<InteractionsHandlerService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<InteractionsHandlerService>());
        services.AddSingleton<CommandHandlerService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<CommandHandlerService>());
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<MessageComponentInteractionsHandler>();
        services.AddSingleton<CollectorService>();

        services.AddSingleton<IInteractionsHandler, DiscordNetInteractionsHandler>();
        services.AddSingleton<IInteractionsHandler, EmbedPlayerDisplayRestoreInteractionsHandler>();
        services.AddSingleton<IInteractionsHandler>(s => s.GetRequiredService<MessageComponentInteractionsHandler>());

        return services;
    }

    public static IServiceCollection AddYandex(this IServiceCollection services, IConfiguration configuration) {
        services.ConfigureNamedOptions<YandexCredentials>(configuration);
        services.AddYandexMusicResolver();
        services.AddSingleton<IMusicResolver, Music.Yandex.YandexMusicResolver>();

        return services;
    }

    public static IServiceCollection AddLavalink(this IServiceCollection builder) {
        builder
            .AddSingleton<INodeBalancingStrategy, EnlivenLavalinkBalancingStrategy>()
            .AddSingleton<IEnlivenClusterAudioService, EnlivenClusterAudioService>()
            .AddSingleton<IClusterAudioService>(provider => provider.GetRequiredService<IEnlivenClusterAudioService>())
            .AddSingleton<IAudioService>(provider => provider.GetRequiredService<IEnlivenClusterAudioService>())
            .AddLavalinkCluster<DiscordClientWrapper>()
            .ConfigureOptions<ClusterAudioServiceOptionsConfigurator>()
            .AddSingleton<IMusicResolver, LavalinkMusicResolver>()
            .AddSingleton<MusicResolverService>()
            .AddInactivityTracking()
            .AddSingleton<IInactivityTracker, UsersInactivityTracker>()
            .AddSingleton<IInactivityTracker, IdleInactivityTracker>()
            .PostConfigure<IdleInactivityTrackerOptions>(options => options.IdleStates = [PlayerState.NotPlaying])
            .ConfigureInactivityTracking((provider, options) => {
                options.DefaultPollInterval = TimeSpan.FromSeconds(30);
                options.DefaultTimeout = TimeSpan.FromSeconds(120);
                options.TimeoutBehavior = InactivityTrackingTimeoutBehavior.Highest;

                options.UseDefaultTrackers = false;
                options.Trackers = provider.GetServices<IInactivityTracker>()
                    .ToImmutableArray();
            });

        builder.RemoveAll(typeof(ILoggerFactory));
        builder.RemoveAll(typeof(ILogger<>));
        builder.RemoveAll(typeof(IConfigureOptions<LoggerFilterOptions>));

        return builder;
    }

    public static IServiceCollection ConfigureNamedOptions<T>(this IServiceCollection services,
        IConfiguration configuration) where T : class {
        return services.Configure<T>(configuration.GetSection(typeof(T).Name));
    }
}