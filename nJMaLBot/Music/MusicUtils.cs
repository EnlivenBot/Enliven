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

namespace Bot.Music {
    public static class MusicUtils {
        public static LavalinkCluster Cluster;

        static MusicUtils() {
            Program.OnClientConnect += (sender, client) => { SetHandler(); };
        }

        private static async Task SetHandler() {
            var nodes = new List<LavalinkNodeOptions>();
            if (GlobalConfig.Instance.IsSelfMusicEnabled) {
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
                Cluster = new LavalinkCluster(new LavalinkClusterOptions {Nodes = nodes.ToArray()}, new DiscordClientWrapper(Program.Client))
                    {StayOnline = true};
                Task.Run(async () => {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    Exception exception = null;
                    do {
                        try {
                            Console.WriteLine("Trying to connect to nodes!");
                            await Cluster.InitializeAsync();
                            exception = null;
                        }
                        catch (Exception e) {
                            exception = e;
                        }
                    } while (exception != null);
                });
            }
        }
    }
}