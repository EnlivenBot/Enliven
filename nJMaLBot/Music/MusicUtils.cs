using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Config;
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

        static MusicUtils() {
            Program.OnClientConnect += (sender, client) => { logger.Swallow(SetHandler()); };
        }

        private static async Task SetHandler() {
            logger.Info("Starting music module");
            var nodes = new List<LavalinkNodeOptions>();
            if (GlobalConfig.Instance.IsSelfMusicEnabled) {
                logger.Info("Preparing self music node");
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Music"));
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Music", "application.yml"))) {
                    logger.Info("Writing default application.yml");
                    WriteResourceToFile("Bot.Music.application.yml", Path.Combine(Directory.GetCurrentDirectory(), "Music", "application.yml"));
                }

                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Music", "lavalink.jar"))) {
                    await DownloadLavalink();
                }
                else if (File.GetCreationTime(Path.Combine(Directory.GetCurrentDirectory(), "Music", "lavalink.jar")) < DateTime.Now.AddDays(-5)) {
                    logger.Swallow(async () => {
                        File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "Music", "lavalink.jar"));
                        await DownloadLavalink();
                    });
                }

                logger.Info("Starting self music node");
                var startInfo = new ProcessStartInfo("java", "-jar lavalink.jar") {
                    CreateNoWindow = false, RedirectStandardError = false, RedirectStandardInput = false,
                    RedirectStandardOutput = false, UseShellExecute = true,
                    WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Music")
                };
                var process = Process.Start(startInfo);
                AppDomain.CurrentDomain.ProcessExit += (sender, args) => process?.Kill();
                nodes.Add(new LavalinkNodeOptions {
                    RestUri = $"http://localhost:{GlobalConfig.Instance.SelfPort}/",
                    WebSocketUri = $"ws://localhost:{GlobalConfig.Instance.SelfPort}/",
                    Password = GlobalConfig.Instance.SelfPass
                });
            }

            nodes.AddRange(GlobalConfig.Instance.LavalinkNodes.Select(instanceLavalinkNode =>
                new LavalinkNodeOptions {
                    RestUri = instanceLavalinkNode.RestUri, WebSocketUri = instanceLavalinkNode.WebSocketUri,
                    Password = instanceLavalinkNode.Password
                }));

            if (nodes.Count != 0) {
                logger.Info("Start building music cluster");
                var lavalinkLogger = new EventLogger();
                lavalinkLogger.LogMessage += (sender, args) => {
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
                try {
                    Cluster = new LavalinkCluster(new LavalinkClusterOptions {Nodes = nodes.ToArray()}, new DiscordClientWrapper(Program.Client),
                        lavalinkLogger) {StayOnline = true};
                    Task.Run(async () => {
                        if (GlobalConfig.Instance.IsSelfMusicEnabled)
                            await Task.Delay(TimeSpan.FromSeconds(10));
                        logger.Info("Trying to connect to nodes!");
                        await Cluster.InitializeAsync();

                        logger.Info("initializing InactivityTrackingService instance with default options");
                        var inactivityTrackingService = new InactivityTrackingService(Cluster, new DiscordClientWrapper(Program.Client),
                            new InactivityTrackingOptions {
                                TrackInactivity = true,
                                DisconnectDelay = TimeSpan.FromSeconds(10),
                                PollInterval = TimeSpan.FromSeconds(4)
                            }, lavalinkLogger);
                    });
                }
                catch (Exception e) {
                    logger.Fatal(e, "Exception with music cluster");
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

        public static async Task<IEnumerable<LavalinkTrack>> GetMusic(IUserMessage message, string query) {
            var lavalinkTracks = new List<LavalinkTrack>();
            if (message.Attachments.Count != 0) {
                foreach (var messageAttachment in message.Attachments) {
                    var lavalinkTrack = await Cluster.GetTrackAsync(messageAttachment.Url);
                    if (lavalinkTrack != null)
                        lavalinkTracks.Add(lavalinkTrack);
                }

                if (lavalinkTracks.Count == 0) {
                    throw new AttachmentAddFailException();
                }
            }
            else if (IsValidUrl(query))
                lavalinkTracks.AddRange(await Cluster.GetTracksAsync(query));
            else {
                var track = await Cluster.GetTrackAsync(query, SearchMode.YouTube);
                if (track != null) lavalinkTracks.Add(track);
            }

            if (lavalinkTracks.Count == 0) {
                throw new TrackNotFoundException();
            }

            return lavalinkTracks;
        }

        public static void WriteResourceToFile(string resourceName, string fileName) {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) {
                using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write)) {
                    resource.CopyTo(file);
                }
            }
        }

        private static async Task DownloadLavalink() {
            logger.Info("Downloading latest lavalink.jar");

            try {
                using (var wc = new WebClient()) {
                    var previousPercent = 0;
                    wc.DownloadProgressChanged += (sender, args) => {
                        logger.Info("Downloading {percentage}%", args.ProgressPercentage);
                        if (args.ProgressPercentage - previousPercent <= 10) return;
                        logger.Info("Downloading - {percentage}%", args.ProgressPercentage);
                        previousPercent = args.ProgressPercentage;
                    };
                    wc.DownloadFileCompleted += (sender, args) => { logger.Info(args.Error, "Download completed - {args}"); };
                    logger.Info("Starting download lavalink.jar");
                    wc.DownloadFile(new Uri("https://gitlab.com/SKProCH/lavalinkci/-/jobs/artifacts/master-jb/raw/Lavalink.jar?job=build"),
                        Path.Combine(Directory.GetCurrentDirectory(), "Music", "lavalink.jar"));
                    logger.Info("End download lavalink.jar");
                }
            }
            catch (Exception e) {
                logger.Fatal(e, "Can't download lavalink");
            }
        }
    }

    public class AttachmentAddFailException : Exception { }

    public class TrackNotFoundException : Exception { }
}