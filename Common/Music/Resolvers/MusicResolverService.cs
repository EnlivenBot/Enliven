using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;

namespace Common.Music.Resolvers {
    public class MusicResolverService {
        private IEnumerable<IMusicResolver> _musicResolvers;

        public MusicResolverService(IEnumerable<IMusicResolver> musicResolvers) {
            _musicResolvers = musicResolvers;
        }

        public MusicResolver GetResolver(string query, LavalinkCluster lavalinkCluster) {
            return new MusicResolver(_musicResolvers, lavalinkCluster, query);
        }
    }
    

    public class MusicResolver {
        private IEnumerable<IMusicResolver> _musicResolvers;
        private LavalinkCluster _cluster;
        private string _query;

        public MusicResolver(IEnumerable<IMusicResolver> musicResolvers, LavalinkCluster cluster, string query) {
            _query = query;
            _cluster = cluster;
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

            return await (await new LavalinkMusicResolver().Resolve(_cluster, _query)).Resolve();
        }
    }
}