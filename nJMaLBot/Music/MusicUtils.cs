using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Logging;
using NLog.Fluent;

#pragma warning disable 4014

namespace Bot.Music {
    public static class MusicUtils {
        public static LavalinkCluster Cluster;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        static MusicUtils() {
            Program.OnClientConnect += (sender, client) => { SetHandler(); };
        }

        private static async Task SetHandler() {
            logger.Info("Starting music module");
            var nodes = new List<LavalinkNodeOptions>();
            if (GlobalConfig.Instance.IsSelfMusicEnabled) {
                logger.Info("Starting self music node");
                var startInfo = new ProcessStartInfo("java", "-jar Lavalink.jar") {
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
                var lavalinkLogger = new Lavalink4NET.Logging.EventLogger();
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
                Cluster = new LavalinkCluster(new LavalinkClusterOptions {Nodes = nodes.ToArray()}, new DiscordClientWrapper(Program.Client), lavalinkLogger)
                    {StayOnline = true};
                Task.Run(async () => {
                    if (GlobalConfig.Instance.IsSelfMusicEnabled)
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    logger.Info("Trying to connect to nodes!");
                    await Cluster.InitializeAsync();
                });
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
    }
}