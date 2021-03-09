using System;
using System.Threading.Tasks;
using Lavalink4NET.Cluster;

namespace Common.Music.Resolvers {
    public interface IMusicResolver {
        Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query);

        Task OnException(LavalinkCluster cluster, string query, Exception e);
    }
}