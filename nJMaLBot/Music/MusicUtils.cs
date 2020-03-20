using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Music.Players;
using Bot.Utilities;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Logging;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Lavalink4NET.Tracking;
using NLog;
using LogLevel = Lavalink4NET.Logging.LogLevel;

#pragma warning disable 4014

namespace Bot.Music {
    public static class MusicUtils {
        public static LavalinkCluster Cluster;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static EventLogger _lavalinkLogger;

        static MusicUtils() {
            Program.OnClientConnect += (sender, client) => { logger.Swallow(SetHandler()); };
            _lavalinkLogger = new EventLogger();
            _lavalinkLogger.LogMessage += (sender, args) => {
                switch (args.Level) {
                    case LogLevel.Information:
                        logger.Info(args.Message);
                        break;
                    case LogLevel.Error:
                        logger.Error(args.Exception, args.Message);
                        break;
                    case LogLevel.Warning:
                        logger.Warn(args.Message);
                        break;
                    case LogLevel.Debug:
                        logger.Info(args.Message);
                        break;
                    case LogLevel.Trace:
                        logger.Trace(args.Message);
                        break;
                    default:
                        logger.Warn(args.Message);
                        break;
                }
            };
        }

        private static async Task SetHandler() {
            logger.Info("Starting music module");
            var nodes = new List<LavalinkNodeOptions>(GlobalConfig.Instance.LavalinkNodes.Select(instanceLavalinkNode =>
                new LavalinkNodeOptions {
                    RestUri = instanceLavalinkNode.RestUri,
                    WebSocketUri = instanceLavalinkNode.WebSocketUri,
                    Password = instanceLavalinkNode.Password
                }));

            if (nodes.Count != 0) {
                logger.Info("Start building music cluster");
                try {
                    Cluster = new LavalinkCluster(new LavalinkClusterOptions {Nodes = nodes.ToArray()}, new DiscordClientWrapper(Program.Client),
                        _lavalinkLogger) {StayOnline = true};
                    Cluster.PlayerMoved += (sender, args) => {
                        var player = args.Player as EmbedPlaybackPlayer;
                        if (!args.CouldBeMoved) {
                            player.PrepareShutdown(player.Loc.Get("Music.PlayerDropped"));
                            player.Dispose();
                        }
                        
                        player.WriteToQueueHistory(player.Loc.Get("Music.PlayerMoved"));
                        return Task.CompletedTask;
                    };
                    logger.Info("Trying to connect to nodes!");
                    await Cluster.InitializeAsync();

                    logger.Info("Initializing InactivityTrackingService instance with default options");
                    var inactivityTrackingService = new InactivityTrackingService(Cluster, new DiscordClientWrapper(Program.Client),
                        new InactivityTrackingOptions {
                            TrackInactivity = true,
                            DisconnectDelay = TimeSpan.FromSeconds(30),
                            PollInterval = TimeSpan.FromSeconds(4)
                        }, _lavalinkLogger);
                    inactivityTrackingService.InactivePlayer += async (sender, args) => {
                        if (args.Player is EmbedPlaybackPlayer embedPlaybackPlayer) {
                            embedPlaybackPlayer.PrepareShutdown(embedPlaybackPlayer.Loc.Get("Music.NoListenersLeft"));
                        }
                    };
                }
                catch (Exception e) {
                    logger.Fatal(e, "Exception with music cluster");
                    Cluster = default;
                }
            }
            else {
                logger.Warn("Nodes not found, music disabled!");
            }
        }

        public static bool IsValidUrl(string query) {
            return Uri.TryCreate(query, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeFile || uriResult.Scheme == Uri.UriSchemeFtp ||
                    uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps ||
                    uriResult.Scheme == Uri.UriSchemeNetTcp);
        }

        public static async Task QueueLoadMusic(IUserMessage message, string query, EmbedPlaybackPlayer player) {
            var lavalinkTracks = new List<LavalinkTrack>();
            if (message != null && message.Attachments.Count != 0) {
                foreach (var messageAttachment in message.Attachments) {
                    var lavalinkTrack = await Cluster.GetTrackAsync(messageAttachment.Url);
                    if (lavalinkTrack != null)
                        lavalinkTracks.Add(lavalinkTrack);
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
                var tasks = query.Split('\n').Select(s => {
                    var validUrl = IsValidUrl(s);
                    return (counter++, s, validUrl ? null : Cluster.GetTrackAsync(s, SearchMode.YouTube), validUrl ? Cluster.GetTracksAsync(s) : null);
                }).ToList();
                await Task.WhenAll(tasks.Select((tuple, i) => (Task) tuple.Item3 ?? tuple.Item4));
                LavalinkTrack lastTrack = null;
                foreach (var (_, s, item3, item4) in tasks.OrderBy(tuple => tuple.Item1)) {
                    if (item4?.Result != null) {
                        lastTrack = null;
                        lavalinkTracks.AddRange(item4.Result);
                    }

                    if (item3?.Result == null) continue;
                    if (lastTrack == null || !lastTrack.Title.Contains(s)) {
                        lavalinkTracks.Add(item3.Result);
                    }

                    lastTrack = item3.Result;
                }
            }

            if (lavalinkTracks.Count == 0) {
                throw new NothingFoundException();
            }

            var tracks = lavalinkTracks
                        .Select(track => {
                             player.WriteToQueueHistory(player.Loc.Get("MusicQueues.Enqueued").Format(message.Author.Username, EscapeTrack(track.Title)));
                             return AuthoredLavalinkTrack.FromLavalinkTrack(track, message.Author);
                         }).ToList();
            await player.PlayAsync(tracks.First(), true);
            player.Playlist.AddRange(tracks.Skip(1));
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