using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;

namespace Common.Music.Resolvers {
    public class MusicResolverService {
        private List<Func<string, LavalinkCluster, IMusicResolver>> _providers = new List<Func<string, LavalinkCluster, IMusicResolver>>();
        public MusicResolver GetResolver(string query, LavalinkCluster cluster) {
            return new MusicResolver(query, cluster, _providers);
        }

        public void RegisterProvider(Func<string, LavalinkCluster, IMusicResolver> func) {
            _providers.Add(func);
        }

        public class MusicResolver {
            private string _query;
            private LavalinkCluster _cluster;
            private List<Func<string, LavalinkCluster, IMusicResolver>> _providers;

            internal MusicResolver(string query, LavalinkCluster cluster, IEnumerable<Func<string, LavalinkCluster, IMusicResolver>> providers) {
                _providers = providers.ToList();
                _cluster = cluster;
                _query = query;
            }

            public async Task<List<LavalinkTrack>> GetTracks() {
                foreach (var musicResolver in _providers.Select(providerFunc => providerFunc(_query, _cluster))) {
                    if (!await musicResolver.CanProvide()) continue;
                    try {
                        return await musicResolver.Provide();
                    }
                    catch (TrackNotFoundException e) {
                        if (!e.AllowFallback) throw;
                    }
                    catch (Exception) {
                        // ignored
                    }
                }

                return await new DefaultMusicResolver(_cluster, _query).Provide();
            }
        }
    }
}