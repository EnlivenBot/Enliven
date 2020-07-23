using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.Music;
using Bot.Utilities;
using NLog;

namespace Bot.DiscordRelated.Music {
    public static class PlayersController {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        // ReSharper disable once NotAccessedField.Local
        private static readonly Thread UpdateThread = new Thread(UpdateCycle);
        public static readonly List<EmbedPlaybackPlayer> PlaybackPlayers = new List<EmbedPlaybackPlayer>();

        static PlayersController() {
            UpdateThread.Priority = ThreadPriority.BelowNormal;
            UpdateThread.Start();
        }

        private static void UpdateCycle() {
            while (true) {
                var waitCycle = Task.Delay(Constants.PlayerEmbedUpdateDelay);
                var embedPlaybackPlayers = PlaybackPlayers.ToList();
                for (var i = 0; i < embedPlaybackPlayers.Count; i++) {
                    var embedPlaybackPlayer = embedPlaybackPlayers[i];
                    if (embedPlaybackPlayer == null) {
                        embedPlaybackPlayers.RemoveAt(i);
                        continue;
                    }

                    try {
                        embedPlaybackPlayer.UpdatePlayer();
                    }
                    catch (Exception e) {
                        if (!(e is ObjectDisposedException))
                            logger.Error(e, "Unhandled exception in player update loop");
                        embedPlaybackPlayers.Remove(embedPlaybackPlayer);
                    }
                }

                waitCycle.GetAwaiter().GetResult();
            }
            // ReSharper disable once FunctionNeverReturns
        }

        public static async Task ForceRemove(ulong guildId, string reason, bool needSave) {
            foreach (var embedPlaybackPlayer in PlaybackPlayers.ToList().Where(player => player.GuildId == guildId)) {
                try {
                    if (String.IsNullOrWhiteSpace(reason)) {
                        await embedPlaybackPlayer.Shutdown(needSave);
                    }
                    else {
                        await embedPlaybackPlayer.Shutdown(reason, needSave);
                    }
                }
                finally {
                    PlaybackPlayers.Remove(embedPlaybackPlayer);
                }
            }
        }

        public static async Task<EmbedPlaybackPlayer> JoinChannel(ulong guildId, ulong voiceChannelId) {
            // Clearing previous player, if we have one
            var oldPlayer = PlaybackPlayers.FirstOrDefault(playbackPlayer => playbackPlayer.GuildId == guildId);
            if (oldPlayer != null) {
                await oldPlayer.Shutdown();
                PlaybackPlayers.Remove(oldPlayer);
            }

            var player = await MusicUtils.Cluster!.JoinAsync(() => new EmbedPlaybackPlayer(guildId), guildId, voiceChannelId);
            player.UpdateNodeName();
            PlaybackPlayers.Add(player);
            return player;
        }
    }
}