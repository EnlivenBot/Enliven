using System.Collections.Generic;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;

namespace Common.Music.Resolvers {
    public interface IMusicResolver {
        Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query);
    }
}