using System.Threading.Tasks;
using Common.Localization.Entries;
using Common.Music.Players;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.Events;
using Lavalink4NET.Integrations;
using Lavalink4NET.Logging;

namespace Common.Music {
    public class EnlivenLavalinkClusterNode : LavalinkClusterNode {
        private static readonly IEntry BotKickedEntry = new EntryLocalized("Music.BotKicked");
        private IDiscordClientWrapper _client;
        public EnlivenLavalinkClusterNode(LavalinkCluster cluster, LavalinkNodeOptions options, IDiscordClientWrapper client, int id, IIntegrationCollection integrationCollection, ILogger? logger = null, ILavalinkCache? cache = null)
            : base(cluster, options, client, id, integrationCollection, logger, cache) {
            _client = client;
        }
        protected override async Task VoiceStateUpdated(object sender, VoiceStateUpdateEventArgs args) {
            if (args.UserId == _client.CurrentUserId) {
                if (Players.TryGetValue(args.VoiceState.GuildId, out var p) && p is FinalLavalinkPlayer player) {
                    var voiceState = args.VoiceState;
                    if (player.VoiceChannelId != null && voiceState.VoiceChannelId is null) {
                        var parameters = new PlayerShutdownParameters() { SavePlaylist = false, ShutdownDisplays = true };
                        await player.Shutdown(BotKickedEntry, parameters);
                    }
                }
            }

            await base.VoiceStateUpdated(sender, args);
        }
    }
}