using Autofac;
using Common.Config;
using Common.Music;
using Common.Music.Controller;
using Common.Music.Encoders;
using Common.Music.Resolvers;
using Lavalink4NET.Lyrics;
using LiteDB;

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
            builder.Register(context => {
                BsonMapper.Global
                    .Entity<UserData>()
                    .DbRef(data => data.PlayerEffects, "PlayerEffects");
                return context.GetDatabase()
                    .GetCollection<UserData>("UserData")
                    .Include(data => data.PlayerEffects);
            }).SingleInstance();
            builder.Register(context => context.GetDatabase().GetCollection<PlayerEffect>("PlayerEffects")).SingleInstance();

            // Music
            builder.RegisterType<MusicResolverService>().SingleInstance();
            builder.RegisterType<MusicController>().As<IMusicController>().As<IService>().SingleInstance();
            builder.RegisterType<LyricsOptions>().SingleInstance();
            builder.RegisterType<LyricsService>().SingleInstance();
            builder.RegisterType<TrackEncoder>().SingleInstance();
            builder.RegisterType<LavalinkTrackEncoder>().AsSelf().AsImplementedInterfaces().SingleInstance();
            
            // Data providers
            builder.RegisterType<UserDataProvider>().As<IUserDataProvider>().SingleInstance();
            builder.RegisterType<StatisticsPartProvider>().As<IStatisticsPartProvider>().SingleInstance();
            builder.RegisterType<GuildConfigProvider>().As<IGuildConfigProvider>().SingleInstance();
            builder.RegisterType<PlaylistProvider>().As<IPlaylistProvider>().SingleInstance();
        }
        
        private static LiteDatabase GetDatabase(this IComponentContext context) {
            return context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult();
        }
    }
}