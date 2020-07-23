using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;

namespace Bot.Utilities.Music {
    public class DefaultMusicProvider : IMusicProvider {
        private LavalinkCluster _cluster;
        private string _query;

        public DefaultMusicProvider(LavalinkCluster cluster, string query) {
            _query = query;
            _cluster = cluster;
        }

        public Task<bool> CanProvide() {
            return Task.FromResult(true);
        }

        public async Task<List<LavalinkTrack>> Provide() {
            if (Utilities.IsValidUrl(_query)) {
                var lavalinkTracks = await _cluster!.GetTracksAsync(_query);
                return lavalinkTracks.ToList();
            }

            // Search two times
            var lavalinkTrack = await _cluster!.GetTrackAsync(_query, SearchMode.YouTube) ?? await _cluster.GetTrackAsync(_query, SearchMode.YouTube);
            return new List<LavalinkTrack> {lavalinkTrack};
        }
    }
}