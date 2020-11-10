using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;
using Lavalink4NET.MemoryCache;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Tyrrrz.Extensions;

namespace Common.Music.Resolvers {
    public class LavalinkMusicResolver : IMusicResolver {
        private static LavalinkCache _lavalinkCache = new LavalinkCache();

        public Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query) {
            return Task.FromResult(new MusicResolveResult(() => Task.FromResult(true), async () => {
                if (_lavalinkCache.TryGetItem(query, out LavalinkTrack cachedTrack)) return new List<LavalinkTrack> {cachedTrack};

                if (Utilities.IsValidUrl(query)) {
                    var lavalinkTracks = await cluster!.GetTracksAsync(query);
                    return lavalinkTracks.ToList();
                }

                // Search two times
                var preferredNode = cluster.GetPreferredNode(NodeRequestType.LoadTrack);
                var lavalinkTrack = await preferredNode.GetTrackAsync(query, SearchMode.YouTube) ??
                                    await (cluster.Nodes.Where(node => node != preferredNode).RandomOrDefault() ?? preferredNode)
                                       .GetTrackAsync(query, SearchMode.YouTube);

                // Add to cache only if request successful
                if (lavalinkTrack != null) {
                    _lavalinkCache.AddItem(query, lavalinkTrack, DateTimeOffset.Now + TimeSpan.FromMinutes(180));
                }

                return new List<LavalinkTrack> {lavalinkTrack!};
            }));
        }
    }
}