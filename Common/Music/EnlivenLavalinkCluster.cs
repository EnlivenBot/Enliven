using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.Logging;

namespace Common.Music
{
    public class EnlivenLavalinkCluster : LavalinkCustomCluster<EnlivenLavalinkClusterNode>
    {
        public EnlivenLavalinkCluster(CustomLavalinkClusterOptions<EnlivenLavalinkClusterNode> options,
            IDiscordClientWrapper client, ILogger? logger = null, ILavalinkCache? cache = null) : base(options, client,
            logger, cache)
        {
        }
    }
}