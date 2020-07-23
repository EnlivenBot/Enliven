using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization.Entries;
using Bot.DiscordRelated.Music;
using Bot.Utilities.Music;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Events;
using Lavalink4NET.Logging;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Lavalink4NET.Tracking;
using NLog;
using LogLevel = Lavalink4NET.Logging.LogLevel;

#pragma warning disable 4014

namespace Bot.Music {
    public static class MusicUtils {
        public static LavalinkCluster Cluster = null!;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static EventLogger _lavalinkLogger = new EventLogger();

        static MusicUtils() {
            logger.Info("Starting music module");

            _lavalinkLogger.LogMessage += LavalinkLoggerOnLogMessage;

            Task.Run(async () => {
                var nodes = new List<LavalinkNodeOptions>(GlobalConfig.Instance.LavalinkNodes.Select(instanceLavalinkNode => instanceLavalinkNode.ToOptions()));
                if (nodes.Count != 0) {
                    logger.Info("Start building music cluster");
                    try {
                        Cluster = new LavalinkCluster(new LavalinkClusterOptions {Nodes = nodes.ToArray()}, new DiscordClientWrapper(Program.Client),
                            _lavalinkLogger) {StayOnline = true};
                        Cluster.PlayerMoved += ClusterOnPlayerMoved;
                        logger.Info("Trying to connect to nodes!");
                        await Cluster.InitializeAsync();

                        logger.Info("Initializing InactivityTrackingService instance with default options");
                        var inactivityTrackingService = new InactivityTrackingService(Cluster, new DiscordClientWrapper(Program.Client),
                            new InactivityTrackingOptions {
                                TrackInactivity = true,
                                DisconnectDelay = TimeSpan.FromSeconds(60),
                                PollInterval = TimeSpan.FromSeconds(4)
                            }, _lavalinkLogger);
                        inactivityTrackingService.InactivePlayer += (sender, args) => {
                            if (args.Player is EmbedPlaybackPlayer embedPlaybackPlayer) {
                                embedPlaybackPlayer.Shutdown(new EntryLocalized("Music.NoListenersLeft"), false);
                            }

                            return Task.CompletedTask;
                        };

                        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                    }
                    catch (Exception e) {
                        logger.Fatal(e, "Exception with music cluster");
                        Cluster = null!;
                    }
                }
                else {
                    logger.Warn("Nodes not found, music disabled!");
                }
            });
        }

        private static void LavalinkLoggerOnLogMessage(object? sender, LogMessageEventArgs e) {
            var logLevel = e.Level switch {
                LogLevel.Information => NLog.LogLevel.Info,
                LogLevel.Error       => NLog.LogLevel.Error,
                LogLevel.Warning     => NLog.LogLevel.Warn,
                LogLevel.Debug       => NLog.LogLevel.Debug,
                LogLevel.Trace       => NLog.LogLevel.Trace,
                _                    => throw new ArgumentOutOfRangeException()
            };
            logger.Log(logLevel, e.Message);
        }

        public static void Initialize() {
            // Dummy method to initialize static properties
        }

        private static void OnProcessExit(object? sender, EventArgs e) {
            if (Cluster == null) return;
            foreach (var embedPlaybackPlayer in Cluster.GetPlayers<EmbedPlaybackPlayer>()) {
                embedPlaybackPlayer.Shutdown();
            }
        }

        private static Task ClusterOnPlayerMoved(object sender, PlayerMovedEventArgs args) {
            var player = args.Player as EmbedPlaybackPlayer;
            if (args.CouldBeMoved) {
                player?.WriteToQueueHistory(player.Loc.Get("Music.PlayerMoved"));
                player?.UpdateNodeName();
            }
            else {
                player?.Shutdown(new EntryLocalized("Music.PlayerDropped"));
            }

            return Task.CompletedTask;
        }

        public static async Task<List<LavalinkTrack>> QueueLoadMusic(IUserMessage message, string? query, EmbedPlaybackPlayer player) {
            var lavalinkTracks = new List<LavalinkTrack>();
            if (message != null && message.Attachments.Count != 0) {
                foreach (var messageAttachment in message.Attachments) {
                    var lavalinkTrack = await MusicProvider.GetTracks(messageAttachment.Url, Cluster);
                    if (lavalinkTrack != null)
                        lavalinkTracks.AddRange(lavalinkTrack);
                }

                if (lavalinkTracks.Count == 0) {
                    throw new AttachmentAddFailException();
                }
            }
            else if (string.IsNullOrWhiteSpace(query)) {
                throw new EmptyQueryException();
            }
            else {
                var counter = 0;
                var tasks = query.Split('\n').Select(s => (counter++, s, MusicProvider.GetTracks(s, Cluster))).ToList();
                await Task.WhenAll(tasks.Select((tuple, i) => tuple.Item3));
                foreach (var (_, _, tracks) in tasks.OrderBy(tuple => tuple.Item1)) {
                    if (tracks?.Result != null) {
                        lavalinkTracks.AddRange(tracks.Result);
                    }
                }
            }

            if (lavalinkTracks.Count == 0) {
                throw new NothingFoundException();
            }
            
            return lavalinkTracks;
        }

        public static string EscapeTrack(string track) {
            track = track.Replace("'", "");
            track = track.Replace("\"", "");
            track = track.Replace("#", "");
            return track;
        }

        public static void EscapeTrack(StringBuilder builder) {
            builder.Replace("'", "");
            builder.Replace("\"", "");
            builder.Replace("#", "");
        }
    }

    public class AttachmentAddFailException : Exception { }

    public class NothingFoundException : Exception { }

    public class EmptyQueryException : Exception { }
}