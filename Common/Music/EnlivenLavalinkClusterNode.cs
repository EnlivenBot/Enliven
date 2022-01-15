using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Localization.Entries;
using Common.Music.Players;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.Events;
using Lavalink4NET.Logging;

namespace Common.Music
{
    public class EnlivenLavalinkClusterNode : LavalinkClusterNode
    {
        private IDiscordClientWrapper _client;
        public EnlivenLavalinkClusterNode(LavalinkNodeOptions options, IDiscordClientWrapper client,
            ILogger? logger = null, ILavalinkCache? cache = null) : base(options, client, logger, cache) {
            _client = client;
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

        private static readonly IEntry BotKickedEntry = new EntryLocalized("Music.BotKicked");
        protected override async Task VoiceStateUpdated(object sender, VoiceStateUpdateEventArgs args) {
            if (args.UserId == _client.CurrentUserId) {
                if (Players.TryGetValue(args.VoiceState.GuildId, out var p) && p is FinalLavalinkPlayer player) {
                    var voiceState = args.VoiceState;
                    if (player.VoiceChannelId != null && voiceState.VoiceChannelId is null) {
                        var parameters = new PlayerShutdownParameters(){SavePlaylist = false, ShutdownDisplays = true};
                        await player.Shutdown(BotKickedEntry, parameters);
                    }
                }
            }

            await base.VoiceStateUpdated(sender, args);
        }
    }
}