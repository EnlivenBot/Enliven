using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;

namespace Common.Music.Resolvers {
    public class MusicResolverService {
        private readonly LavalinkMusicResolver _lavalinkMusicResolver;
        private readonly IEnumerable<IMusicResolver> _musicResolvers;

        public MusicResolverService(IEnumerable<IMusicResolver> musicResolvers, LavalinkMusicResolver lavalinkMusicResolver) {
            _musicResolvers = musicResolvers;
            _lavalinkMusicResolver = lavalinkMusicResolver;
        }

        public MusicResolver GetResolver(string query, LavalinkCluster lavalinkCluster) {
            return new MusicResolver(_musicResolvers, lavalinkCluster, _lavalinkMusicResolver, query);
        }
    }


    public class MusicResolver {
        private readonly LavalinkCluster _cluster;
        private readonly LavalinkMusicResolver _lavalinkMusicResolver;
        private readonly IEnumerable<IMusicResolver> _musicResolvers;
        private readonly string _query;

        public MusicResolver(IEnumerable<IMusicResolver> musicResolvers, LavalinkCluster cluster, LavalinkMusicResolver lavalinkMusicResolver, string query) {
            _query = query;
            _cluster = cluster;
            _lavalinkMusicResolver = lavalinkMusicResolver;
            _musicResolvers = musicResolvers;
        }

        public async Task<ICollection<LavalinkTrack>> GetTracks() {
            foreach (var resolver in _musicResolvers) {
                try {
                    var lavalinkTracks = await resolver.Resolve(_cluster, _query)
                        .PipeAsync(enumerable => enumerable.ToList());
                    if (lavalinkTracks.Count != 0) return lavalinkTracks;
                }
                catch (TrackNotFoundException e) {
                    if (!e.AllowFallback) throw;
                }
                catch (Exception) {
                    // ignored
                }
            }

            return await _lavalinkMusicResolver.Resolve(_cluster, _query)
                .PipeAsync(tracks => tracks.ToList());
        }
    }
}