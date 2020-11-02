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
    public class DefaultMusicResolver : IMusicResolver {
        private static LavalinkCache _lavalinkCache = new LavalinkCache();
        private LavalinkCluster _cluster;
        private string _query;

        public DefaultMusicResolver(LavalinkCluster cluster, string query) {
            _query = query;
            _cluster = cluster;
        }

        public Task<bool> CanProvide() {
            return Task.FromResult(true);
        }

        public async Task<List<LavalinkTrack>> Provide() {
            if (_lavalinkCache.TryGetItem(_query, out LavalinkTrack cachedTrack)) return new List<LavalinkTrack> {cachedTrack};

            if (Utilities.IsValidUrl(_query)) {
                var lavalinkTracks = await _cluster!.GetTracksAsync(_query);
                return lavalinkTracks.ToList();
            }

            // Search two times
            var preferredNode = _cluster.GetPreferredNode(NodeRequestType.LoadTrack);
            var lavalinkTrack = await preferredNode.GetTrackAsync(_query, SearchMode.YouTube) ??
                                await (_cluster.Nodes.Where(node => node != preferredNode).RandomOrDefault() ?? preferredNode)
                                   .GetTrackAsync(_query, SearchMode.YouTube);

            // Add to cache only if request successful
            if (lavalinkTrack != null) {
                _lavalinkCache.AddItem(_query, lavalinkTrack, DateTimeOffset.Now + TimeSpan.FromMinutes(180));
            }

            return new List<LavalinkTrack> {lavalinkTrack!};
        }
    }
}