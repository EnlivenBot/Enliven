using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;

namespace Common.Music.Resolvers {
    public class MusicResolverService {
        private readonly IEnumerable<IMusicResolver> _musicResolvers;
        private readonly LavalinkMusicResolver _lavalinkMusicResolver;

        public MusicResolverService(IEnumerable<IMusicResolver> musicResolvers, LavalinkMusicResolver lavalinkMusicResolver) {
            _musicResolvers = musicResolvers;
            _lavalinkMusicResolver = lavalinkMusicResolver;
        }

        public MusicResolver GetResolver(string query, LavalinkCluster lavalinkCluster) {
            return new MusicResolver(_musicResolvers, lavalinkCluster, _lavalinkMusicResolver, query);
        }
    }
    

    public class MusicResolver {
        private readonly IEnumerable<IMusicResolver> _musicResolvers;
        private readonly LavalinkCluster _cluster;
        private readonly LavalinkMusicResolver _lavalinkMusicResolver;
        private readonly string _query;

        public MusicResolver(IEnumerable<IMusicResolver> musicResolvers, LavalinkCluster cluster, LavalinkMusicResolver lavalinkMusicResolver, string query) {
            _query = query;
            _cluster = cluster;
            _lavalinkMusicResolver = lavalinkMusicResolver;
            _musicResolvers = musicResolvers;
        }

        public async Task<List<LavalinkTrack>> GetTracks() {
            foreach (var musicResolverTask in _musicResolvers.Select(resolver => resolver.Resolve(_cluster, _query))) {
                var musicResolver = await musicResolverTask;
                if (!await musicResolver.CanResolve()) continue;
                try {
                    return await musicResolver.Resolve();
                }
                catch (TrackNotFoundException e) {
                    if (!e.AllowFallback) throw;
                }
                catch (Exception) {
                    // ignored
                }
            }

            return await _lavalinkMusicResolver
                .Resolve(_cluster, _query)
                .PipeAsync(result => result.Resolve());
        }
    }
}