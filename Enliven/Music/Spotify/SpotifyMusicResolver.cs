using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Music;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using SpotifyAPI.Web;

#pragma warning disable 1998

#pragma warning disable 8604
#pragma warning disable 8602

namespace Bot.Music.Spotify {
    public class SpotifyMusicResolver : IMusicResolver, ISpotifyAssociationCreator {
        private readonly ISpotifyAssociationProvider _spotifyAssociationProvider;
        private readonly SpotifyClientResolver _spotifyClientResolver;
        private readonly LavalinkMusicResolver _lavalinkMusicResolver;

        public SpotifyMusicResolver(ISpotifyAssociationProvider spotifyAssociationProvider, SpotifyClientResolver spotifyClientResolver, LavalinkMusicResolver lavalinkMusicResolver) {
            _spotifyClientResolver = spotifyClientResolver;
            _lavalinkMusicResolver = lavalinkMusicResolver;
            _spotifyAssociationProvider = spotifyAssociationProvider;
        }

        public Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query) {
            var url = new SpotifyUrl(query);
            return Resolve(cluster, url);
        }

        public async Task<MusicResolveResult> Resolve(LavalinkCluster cluster, SpotifyUrl url) {
            return new MusicResolveResult(
                async () => await _spotifyClientResolver.GetSpotify() != null && url.IsValid,
                () => Resolve(url, cluster)
            );
        }

        public Task OnException(LavalinkCluster cluster, string query, Exception e)
        {
            return Task.CompletedTask;
        }

        public async Task<SpotifyAssociation?> ResolveAssociation(SpotifyTrackWrapper spotifyTrackWrapper, LavalinkCluster lavalinkCluster) {
            var cachedTrack = _spotifyAssociationProvider.Get(spotifyTrackWrapper.Id);
            if (cachedTrack != null) return cachedTrack;

            try {
                var spotifyClient = (await _spotifyClientResolver.GetSpotify())!;
                var trackInfo = await spotifyTrackWrapper.GetTrackInfo(spotifyClient);
                var lavalinkTracks = await _lavalinkMusicResolver
                    .Pipe(resolver => resolver.Resolve(lavalinkCluster, trackInfo))
                    .PipeAsync(result => result.Resolve());
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
                var spotify = (await _spotifyClientResolver.GetSpotify())!;
                var spotifyTracks = await url.Resolve(spotify);
                var enumerable = spotifyTracks
                    .Select(async s => {
                            var association = await ResolveAssociation(s, cluster);
                            return association != null ? new SpotifyLavalinkTrack(s, association.GetBestAssociation().Association) : null;
                        }
                    ).ToList();
                return await Task.WhenAll(enumerable)
                    .PipeAsync(tracks => tracks
                        .Where(track => track != null)
                        // ReSharper disable once SuspiciousTypeConversion.Global
                        .Cast<LavalinkTrack>()
                        .ToList()
                    );
            }
            catch (Exception) {
                throw new TrackNotFoundException(false);
            }
        }
    }
}