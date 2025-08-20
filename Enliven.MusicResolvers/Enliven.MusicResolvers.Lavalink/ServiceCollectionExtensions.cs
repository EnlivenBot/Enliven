using Common.Config;
using Common.Music;
using Enliven.MusicResolvers.Base;
using Enliven.MusicResolvers.Lavalink.Resolvers;
using Lavalink4NET.Cluster.Extensions;
using Lavalink4NET.Cluster.LoadBalancing.Strategies;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.InactivityTracking;
using Lavalink4NET.InactivityTracking.Extensions;
using Lavalink4NET.InactivityTracking.Trackers;
using Lavalink4NET.InactivityTracking.Trackers.Idle;
using Lavalink4NET.InactivityTracking.Trackers.Users;
using Lavalink4NET.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Enliven.MusicResolvers.Lavalink;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddLavalink(this IServiceCollection builder) {
        builder
            .AddSingleton<INodeBalancingStrategy, EnlivenLavalinkBalancingStrategy>()
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
                options.Trackers = [..provider.GetServices<IInactivityTracker>()];
            });

        builder.RemoveAll(typeof(ILoggerFactory));
        builder.RemoveAll(typeof(ILogger<>));
        builder.RemoveAll(typeof(IConfigureOptions<LoggerFilterOptions>));

        return builder;
    }

    public static IServiceCollection AddDeezer(this IServiceCollection builder) {
        builder.AddSingleton<IMusicResolver, DeezerMusicResolver>();

        return builder;
    }
}