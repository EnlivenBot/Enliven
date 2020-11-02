using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Config;
using Common.Music;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using LiteDB;
using NLog;
using SpotifyAPI.Web;

#pragma warning disable 8604
#pragma warning disable 8602

namespace Bot.Utilities.Music {
    public class SpotifyMusicResolver : IMusicResolver {
        private static readonly ILiteCollection<SpotifyTrackAssociation> SpotifyAssociations =
            Database.LiteDatabase.GetCollection<SpotifyTrackAssociation>(@"SpotifyAssociations");
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private LavalinkCluster _cluster;

        static SpotifyMusicResolver() {
            SpotifyClient = Task.Run(async () => {
                try {
                    var config = SpotifyClientConfig.CreateDefault();

                    var request = new ClientCredentialsRequest(GlobalConfig.Instance.SpotifyClientID, GlobalConfig.Instance.SpotifyClientSecret);
                    // If credentials wrong, this \/ line will throw the exception
                    await new OAuthClient(config).RequestToken(request);

                    var actualConfig = SpotifyClientConfig
                                      .CreateDefault()
                                      .WithAuthenticator(new ClientCredentialsAuthenticator(GlobalConfig.Instance.SpotifyClientID,
                                           GlobalConfig.Instance.SpotifyClientSecret));
                    return new SpotifyClient(actualConfig);
                }
                catch (Exception e) {
                    logger.Error(e, "Wrong Spotify credentials. Check config file");
                    return null;
                }
            });
        }

        public SpotifyMusicResolver(LavalinkCluster cluster, string query) {
            _cluster = cluster;
            Query = new SpotifyUrl(query);
        }


        public static Task<SpotifyClient?> SpotifyClient { get; private set; }

        public SpotifyUrl Query { get; private set; }

        public async Task<bool> CanProvide() {
            return await SpotifyClient != null && Query.IsValid;
        }

        public async Task<List<LavalinkTrack>> Provide() {
            try {
                var spotify = await SpotifyClient;
                var spotifyTracks = await Query.Resolve(spotify);
                var enumerable = spotifyTracks.Select(async s =>
                    new SpotifyLavalinkTrack(s, (await ResolveWithCache(s, _cluster)).GetBestAssociation().Association)).ToList();
                return (await Task.WhenAll(enumerable)).Cast<LavalinkTrack>().ToList();
            }
            catch (Exception) {
                throw new TrackNotFoundException(false);
            }
        }

        public static void Initialize() {
            // Dummy method to initialize static properties
        }

        public static async Task<SpotifyTrackAssociation?> ResolveWithCache(SpotifyTrack spotifyTrack, LavalinkCluster lavalinkCluster) {
            if (SpotifyTrackAssociation.TryGet(spotifyTrack.Id, out var track)) {
                return track!;
            }

            try {
                var lavalinkTracks = await new DefaultMusicResolver(lavalinkCluster, await spotifyTrack.GetTrackInfo()).Provide();
                var spotifyTrackAssociation = new SpotifyTrackAssociation(spotifyTrack.Id, lavalinkTracks[0].Identifier);
                spotifyTrackAssociation.Save();
                return spotifyTrackAssociation;
            }
            catch (Exception) {
                // ignored
            }

            return null;
        }
    }
}