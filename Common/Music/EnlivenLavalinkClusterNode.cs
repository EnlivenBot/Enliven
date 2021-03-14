using System.Collections.Generic;
using System.Linq;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.Logging;

namespace Common.Music
{
    public class EnlivenLavalinkClusterNode : LavalinkClusterNode
    {
        public EnlivenLavalinkClusterNode(LavalinkNodeOptions options, IDiscordClientWrapper client,
            ILogger? logger = null, ILavalinkCache? cache = null) : base(options, client, logger, cache)
        {
            if (options.Label != null)
            {
                var strings = options.Label.Split("|");
                Label = strings.Length > 1 ? strings.Last() : options.Label ?? Label;

                if (strings.Length > 1)
                {
                    Tags = strings.SkipLast(1).ToList();
                }
            }
        }

        public List<string> Tags { get; set; } = new List<string>();

        public EnlivenLavalinkCluster GetCluster()
        {
            return (EnlivenLavalinkCluster) Cluster;
        }
    }
}