using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Music.Players;
using Common.Music.Resolvers;

namespace Common.Music.Controller {
    public interface IMusicController : IService {
        public bool IsMusicEnabled { get; set; }
        
        public EnlivenLavalinkCluster Cluster { get; set; }

        public Task<FinalLavalinkPlayer> ProvidePlayer(ulong guildId, ulong voiceChannelId, bool recreate = false);
        
        public Task<FinalLavalinkPlayer> CreatePlayer(PlayerShutdownParameters parameters);

        public void StoreShutdownParameters(PlayerShutdownParameters parameters);

        /// <summary>
        /// Attempts to restore the player using the latest available PlayerShutdownParameters for a specific guild.
        /// If the player already exists, return it
        /// </summary>
        /// <param name="guildId">Target guild id</param>
        /// <returns>Player instance or null if error</returns>
        public Task<FinalLavalinkPlayer?> RestoreLastPlayer(ulong guildId);

        public FinalLavalinkPlayer? GetPlayer(ulong guildId);

        public Task<IEnumerable<MusicResolver>> ResolveQueries(IEnumerable<string> queries);
    }
}