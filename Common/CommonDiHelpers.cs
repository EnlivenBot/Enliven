using System;
using System.Linq;
using System.Net.Http;
using Autofac;
using Autofac.Builder;
using Autofac.Core.Lifetime;
using Common.Config;
using Common.Music;
using Common.Music.Controller;
using Common.Music.Encoders;
using Common.Music.Resolvers;
using Lavalink4NET.Lyrics;
using LiteDB;

namespace Common {
    public static class CommonDiHelpers {
        public static ContainerBuilder AddCommonServices(this ContainerBuilder builder) {
            builder.RegisterType<HttpClient>().SingleInstance();

            // Database related
            builder.RegisterType<LiteDatabaseProvider>().SingleInstance();

            builder.Register(context => context.GetDatabase().GetCollection<Entity>(@"Global")).SingleInstance();
            builder.Register(context => context.GetDatabase().GetCollection<GuildConfig>(@"Guilds")).SingleInstance();
            builder.Register(context => context.GetDatabase().GetCollection<StatisticsPart>(@"CommandStatistics")).SingleInstance();
            builder.Register(context => context.GetDatabase().GetCollection<StoredPlaylist>(@"StoredPlaylists")).SingleInstance();
            builder
                .Register(context => context.GetDatabase().GetCollection<UserData>("UserData").Include(data => data.PlayerEffects))
                .OnRegistered(args => BsonMapper.Global.Entity<UserData>().DbRef(data => data.PlayerEffects, "PlayerEffects"))
                .SingleInstance();
            builder.Register(context => context.GetDatabase().GetCollection<PlayerEffect>("PlayerEffects")).SingleInstance();

            // Music
            builder.RegisterType<MusicResolverService>().SingleInstance();
            builder.RegisterType<LyricsOptions>().SingleInstance();
            builder.RegisterType<LyricsService>().SingleInstance();
            builder.RegisterType<TrackEncoderUtils>().SingleInstance();
            builder.RegisterType<LavalinkTrackEncoderUtil>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<LavalinkMusicResolver>().AsSelf().SingleInstance();

            // Data providers
            builder.RegisterType<UserDataProvider>().As<IUserDataProvider>().SingleInstance();
            builder.RegisterType<StatisticsPartProvider>().As<IStatisticsPartProvider>().SingleInstance();
            builder.RegisterType<GuildConfigProvider>().As<IGuildConfigProvider>().SingleInstance();
            builder.RegisterType<PlaylistProvider>().As<IPlaylistProvider>().SingleInstance();

            return builder;
        }

        public static LiteDatabase GetDatabase(this IComponentContext context) {
            return context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult();
        }

        public static IRegistrationBuilder<TLimit, TActivatorData, TStyle> InstancePerBot<TLimit, TActivatorData, TStyle>(
            this IRegistrationBuilder<TLimit, TActivatorData, TStyle> registration, params object[] lifetimeScopeTags) {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            var tags = new[] { Constants.BotLifetimeScopeTag }.Concat(lifetimeScopeTags).ToArray();
            return registration.InstancePerMatchingLifetimeScope(tags);
        }
    }
}