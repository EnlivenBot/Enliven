using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Bot.Music.Players {
    public static class EmbedPlaybackControl {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static Timer _timer;
        private static readonly Thread UpdateThread = new Thread(Update);
        public static readonly ObservableCollection<EmbedPlaybackPlayer> PlaybackPlayers = new ObservableCollection<EmbedPlaybackPlayer>();

        static EmbedPlaybackControl() {
            UpdateThread.Priority = ThreadPriority.BelowNormal;
            UpdateThread.Start();
        }

        private static void Update() {
            while (true) {
                var waitCycle = WaitCycle();
                var embedPlaybackPlayers = PlaybackPlayers.ToList();
                for (var i = 0; i < embedPlaybackPlayers.Count; i++) {
                    var embedPlaybackPlayer = embedPlaybackPlayers[i];
                    try {
                        if (embedPlaybackPlayer.UpdatePlayback) {
                            embedPlaybackPlayer.UpdateProgress(true);
                        }
                    }
                    catch (Exception e) {
                        logger.Error(e, "Exception while updating playback");
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
                    NotifyCollectionChangedEventHandler playbackPlayersOnCollectionChanged = null;
                    playbackPlayersOnCollectionChanged = new NotifyCollectionChangedEventHandler((sender, args) => {
                        PlaybackPlayers.CollectionChanged -= playbackPlayersOnCollectionChanged;
                        tcs.SetResult(true);
                    });
                    PlaybackPlayers.CollectionChanged += playbackPlayersOnCollectionChanged;
                }
                else {
                    tcs.SetResult(true);
                }
            }, null, 4000, 0);
            return tcs.Task;
        }
    }
}