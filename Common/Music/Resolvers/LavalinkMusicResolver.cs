using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;
using Lavalink4NET.MemoryCache;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using NLog;
using Tyrrrz.Extensions;

namespace Common.Music.Resolvers {
    public class LavalinkMusicResolver : IMusicResolver
    {
        private static ILogger _logger = LogManager.GetCurrentClassLogger();
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
                                    await (cluster.Nodes.Where(node => node != preferredNode && node.IsConnected).RandomOrDefault() ?? preferredNode)
                                       .GetTrackAsync(query, SearchMode.YouTube);

                // Add to cache only if request successful
                if (lavalinkTrack == null) return new List<LavalinkTrack>();
                _lavalinkCache.AddItem(query, lavalinkTrack, DateTimeOffset.Now + TimeSpan.FromMinutes(180));
                return new List<LavalinkTrack> {lavalinkTrack};
            }));
        }

        public Task OnException(LavalinkCluster cluster, string query, Exception e)
        {
            _logger.Warn(e, "Lavalink resolve failed. Query: {0}", query);
            return Task.CompletedTask;
        }
    }
}