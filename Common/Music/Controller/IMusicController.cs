using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Music.Players;
using Common.Music.Resolvers;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using NLog;

namespace Common.Music.Controller {
    public interface IMusicController : IService {
        public LavalinkCluster Cluster { get; set; }

        public Task<FinalLavalinkPlayer> ProvidePlayer(ulong guildId, ulong voiceChannelId, bool recreate = false);

        public FinalLavalinkPlayer? GetPlayer(ulong guildId);

        public Task<IEnumerable<MusicResolver>> ResolveQueries(IEnumerable<string> queries);
    }
}