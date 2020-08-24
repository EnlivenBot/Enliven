using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Music;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;

namespace Bot.Utilities.Music {
    public class MusicProvider {
        public static async Task<IMusicProvider?> GetProvider(string query, LavalinkCluster cluster) {
            // Other providers goes here
            var spotifyMusicProvider = new SpotifyMusicProvider(cluster, query);
            if (await spotifyMusicProvider.CanProvide()) {
                return spotifyMusicProvider;
            }

            // Return null if none of them can provide result
            return null;
        }
        
        public static async Task<List<LavalinkTrack>> GetTracks(string query, LavalinkCluster cluster) {
            try {
                var musicProvider = await GetProvider(query, cluster);
                if (musicProvider != null) {
                    return await musicProvider.Provide();
                }
            }
            catch (NothingFoundException e) {
                if (!e.AllowFallback) throw;
            }
            catch (Exception) {
                // ignored
            }
            
            return await new DefaultMusicProvider(cluster, query).Provide();
        }
    }
}