using Autofac;
using Common.Config;
using Common.Music;
using Common.Music.Controller;
using Common.Music.Players;
using Common.Music.Resolvers;

namespace Common
{
    public static class Startup
    {
        public static void ConfigureServices(ContainerBuilder builder)
        {
            // Database related
            builder.RegisterType<LiteDatabaseProvider>().SingleInstance();

            builder.Register(context =>
                context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                    .GetCollection<Entity>(@"Global")).SingleInstance();
            builder.Register(context =>
                context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                    .GetCollection<GuildConfig>(@"Guilds")).SingleInstance();
            builder.Register(context =>
                context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                    .GetCollection<StatisticsPart>(@"CommandStatistics")).SingleInstance();
            builder.Register(context =>
                context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                    .GetCollection<StoredPlaylist>(@"StoredPlaylists")).SingleInstance();
            builder.Register(context =>
                context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                    .GetCollection<UserData>("UserData")).SingleInstance();
            
            
            // Music
            builder.RegisterType<MusicResolverService>().SingleInstance();
            builder.RegisterType<MusicController>().As<IMusicController>().As<IService>().SingleInstance();
            
            // Data providers
            builder.RegisterType<UserDataProvider>().As<IUserDataProvider>().SingleInstance();
            builder.RegisterType<StatisticsPartProvider>().As<IStatisticsPartProvider>().SingleInstance();
            builder.RegisterType<GuildConfigProvider>().As<IGuildConfigProvider>().SingleInstance();
            builder.RegisterType<PlaylistProvider>().As<IPlaylistProvider>().SingleInstance();
        }
    }
}