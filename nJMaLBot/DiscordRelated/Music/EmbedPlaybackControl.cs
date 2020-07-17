using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Bot.DiscordRelated.Music {
    public static class EmbedPlaybackControl {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        // ReSharper disable once NotAccessedField.Local
        private static Timer _timer = null!;
        private static readonly Thread UpdateThread = new Thread(UpdateCycle);
        public static readonly ObservableCollection<EmbedPlaybackPlayer> PlaybackPlayers = new ObservableCollection<EmbedPlaybackPlayer>();

        static EmbedPlaybackControl() {
            UpdateThread.Priority = ThreadPriority.BelowNormal;
            UpdateThread.Start();
        }

        private static void UpdateCycle() {
            while (true) {
                var waitCycle = WaitCycle();
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

        private static Task<bool> WaitCycle() {
            var tcs = new TaskCompletionSource<bool>();
            _timer = new Timer(state => {
                if (PlaybackPlayers.Count == 0) {
                    void PlaybackPlayersOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args) {
                        PlaybackPlayers.CollectionChanged -= PlaybackPlayersOnCollectionChanged;
                        tcs.SetResult(true);
                    }

                    PlaybackPlayers.CollectionChanged += PlaybackPlayersOnCollectionChanged;
                }
                else {
                    tcs.SetResult(true);
                }
            }, null, 4000, 0);
            return tcs.Task;
        }

        public static async Task ForceRemove(ulong guildId, string reason, bool needSave) {
            foreach (var embedPlaybackPlayer in PlaybackPlayers.ToList().Where(player => player.GuildId == guildId)) {
                try {
                    if (string.IsNullOrWhiteSpace(reason)) {
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
    }
}