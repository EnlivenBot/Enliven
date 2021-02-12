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

        public SpotifyMusicResolver(ISpotifyAssociationProvider spotifyAssociationProvider, SpotifyClientResolver client)
        {
            SpotifyClient = client.GetSpotify();
            _spotifyAssociationProvider = spotifyAssociationProvider;
        }

        public Task<SpotifyClient?> SpotifyClient { get; }

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
                var lavalinkTracks = await (await new LavalinkMusicResolver().Resolve(lavalinkCluster, await spotifyTrackWrapper.GetTrackInfo(await SpotifyClient))).Resolve();
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