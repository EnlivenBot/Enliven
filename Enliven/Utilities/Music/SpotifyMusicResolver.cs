using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Config;
using Common.Music;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using NLog;
using SpotifyAPI.Web;

#pragma warning disable 1998

#pragma warning disable 8604
#pragma warning disable 8602

namespace Bot.Utilities.Music {
    public interface ISpotifyAssociationCreator {
        Task<SpotifyAssociation?> ResolveAssociation(SpotifyTrackWrapper spotifyTrackWrapper, LavalinkCluster lavalinkCluster);
    }

    public class SpotifyMusicResolver : IMusicResolver, ISpotifyAssociationCreator {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private ISpotifyAssociationProvider _spotifyAssociationProvider;

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

        public SpotifyMusicResolver(ISpotifyAssociationProvider spotifyAssociationProvider) {
            _spotifyAssociationProvider = spotifyAssociationProvider;
        }

        public static Task<SpotifyClient?> SpotifyClient { get; }

        public async Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query) {
            var url = new SpotifyUrl(query);
            return new MusicResolveResult(
                async () => await SpotifyClient != null && url.IsValid,
                () => Resolve(url, cluster)
            );
        }

        public async Task<SpotifyAssociation?> ResolveAssociation(SpotifyTrackWrapper spotifyTrackWrapper, LavalinkCluster lavalinkCluster) {
            var cachedTrack = _spotifyAssociationProvider.Get(spotifyTrackWrapper.Id);
            if (cachedTrack != null) return cachedTrack;

            try {
                var lavalinkTracks = await (await new LavalinkMusicResolver().Resolve(lavalinkCluster, await spotifyTrackWrapper.GetTrackInfo())).Resolve();
                var spotifyTrackAssociation = _spotifyAssociationProvider.Create(spotifyTrackWrapper.Id, lavalinkTracks[0].Identifier);
                spotifyTrackAssociation.Save();
                return spotifyTrackAssociation;
            }
            catch (Exception) {
                // ignored
            }

            return null;
        }

        private async Task<List<LavalinkTrack>> Resolve(SpotifyUrl url, LavalinkCluster cluster) {
            try {
                var spotify = await SpotifyClient;
                var spotifyTracks = await url.Resolve(spotify);
                var enumerable = spotifyTracks.Select(async s =>
                    new SpotifyLavalinkTrack(s, (await ResolveAssociation(s, cluster)).GetBestAssociation().Association)).ToList();
                return (await Task.WhenAll(enumerable)).Cast<LavalinkTrack>().ToList();
            }
            catch (Exception) {
                throw new TrackNotFoundException(false);
            }
        }
    }
}