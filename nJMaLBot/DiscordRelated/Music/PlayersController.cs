using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config.Localization.Entries;
using Bot.Music;
using Bot.Utilities;
using NLog;

namespace Bot.DiscordRelated.Music {
    public static class PlayersController {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        // ReSharper disable once NotAccessedField.Local
        private static readonly Thread UpdateThread = new Thread(UpdateCycle);
        private static readonly List<EmbedPlaybackPlayer> PlaybackPlayers = new List<EmbedPlaybackPlayer>();

        static PlayersController() {
            UpdateThread.Priority = ThreadPriority.BelowNormal;
            UpdateThread.Start();
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        private static void OnProcessExit(object? sender, EventArgs e) {
            foreach (var player in PlaybackPlayers.ToList()) {
                player.ExecuteShutdown();
            }
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

        public static async Task<EmbedPlaybackPlayer> ProvidePlayer(ulong guildId, ulong voiceChannelId, bool recreate = false) {
            var oldPlayer = PlaybackPlayers.FirstOrDefault(playbackPlayer => playbackPlayer.GuildId == guildId);
            if (oldPlayer != null) {
                if (!recreate) return oldPlayer;
                if (!oldPlayer.IsShutdowned) {
                    await oldPlayer.ExecuteShutdown();
                }
                PlaybackPlayers.Remove(oldPlayer);
            }
            
            var player = await MusicUtils.Cluster!.JoinAsync(() => new EmbedPlaybackPlayer(guildId), guildId, voiceChannelId);
            player.Shutdown += PlayerOnShutdown;
            player.UpdateNodeName();
            PlaybackPlayers.Add(player);
            return player;
        }

        private static void PlayerOnShutdown(object? sender, IEntry e) {
            var player = (sender as EmbedPlaybackPlayer)!;
            player.Shutdown -= PlayerOnShutdown;
            PlaybackPlayers.Remove(player);
        }

        public static EmbedPlaybackPlayer? GetPlayer(ulong guildId) {
            var embedPlaybackPlayer = PlaybackPlayers.FirstOrDefault(player => player.GuildId == guildId);
            return embedPlaybackPlayer?.IsShutdowned == true ? null : embedPlaybackPlayer;
        }
    }
}