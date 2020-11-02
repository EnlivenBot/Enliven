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
    public interface IMusicController {
        public LavalinkCluster Cluster { get; set; }
        
        Task InitializeAsync(List<LavalinkNodeOptions> nodes, IDiscordClientWrapper wrapper, ILogger? logger);

        public Task<FinalLavalinkPlayer> ProvidePlayer(ulong guildId, ulong voiceChannelId, bool recreate = false);

        public FinalLavalinkPlayer? GetPlayer(ulong guildId);

        public Task<IEnumerable<MusicResolverService.MusicResolver>> ResolveQueries(IEnumerable<string> queries);
    }
}