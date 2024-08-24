using System;
using Common.Config;
using Common.Music;
using Common.Music.Effects;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;

namespace Common;

public static class CommonDiHelpers
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        BsonMapper.Global.Entity<UserData>().DbRef(data => data.PlayerEffects, "PlayerEffects");
        return services.AddSingleton<LiteDatabaseProvider>()
            .AddSingleton(context => context.GetDatabase().GetCollection<Entity>(@"Global"))
            .AddSingleton(context => context.GetDatabase().GetCollection<GuildConfig>(@"Guilds"))
            .AddSingleton(context => context.GetDatabase().GetCollection<StatisticsPart>(@"CommandStatistics"))
            .AddSingleton(context => context.GetDatabase().GetCollection<StoredPlaylist>(@"StoredPlaylists"))
            .AddSingleton(context => context.GetDatabase().GetCollection<UserData>("UserData")
                .Include(data => data.PlayerEffects))
            .AddSingleton(context => context.GetDatabase().GetCollection<PlayerEffect>("PlayerEffects"))
            .AddSingleton<IUserDataProvider, UserDataProvider>()
            .AddSingleton<IStatisticsPartProvider, StatisticsPartProvider>()
            .AddSingleton<IGuildConfigProvider, GuildConfigProvider>()
            .AddSingleton<IPlaylistProvider, PlaylistProvider>()
            .AddSingleton<EffectSourceProvider>();
    }

    private static LiteDatabase GetDatabase(this IServiceProvider context)
    {
        return context.GetRequiredService<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult();
    }
}