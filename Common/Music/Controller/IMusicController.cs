using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Music.Players;
using Common.Music.Resolvers;

namespace Common.Music.Controller {
    public interface IMusicController : IService {
        public bool IsMusicEnabled { get; }
        public Task<EnlivenLavalinkCluster> ClusterTask { get; }

        public Task<FinalLavalinkPlayer> ProvidePlayer(ulong guildId, ulong voiceChannelId, bool recreate = false);

        public void StoreSnapshot(PlayerSnapshot parameters);

        public FinalLavalinkPlayer? GetPlayer(ulong guildId);

        public Task<IEnumerable<MusicResolver>> ResolveQueries(IEnumerable<string> queries);
        PlayerSnapshot? GetPlayerLastSnapshot(ulong guildId);
    }
}