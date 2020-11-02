using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Players;
using Common.Music.Resolvers;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.Events;
using Lavalink4NET.Logging;
using Lavalink4NET.Tracking;
using ILogger = NLog.ILogger;

namespace Common.Music.Controller {
    public class MusicController : IMusicController, IDisposable {
        private static readonly List<FinalLavalinkPlayer> PlaybackPlayers = new List<FinalLavalinkPlayer>();
        private static EventLogger _lavalinkLogger = new EventLogger();
        private MusicResolverService _musicResolverService;

        public MusicController(MusicResolverService musicResolverService) {
            _musicResolverService = musicResolverService;
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => { Dispose(); };
        }

        public void Dispose() {
            foreach (var player in PlaybackPlayers.ToList()) {
                player.ExecuteShutdown(new PlayerShutdownParameters());
            }
        }

        public LavalinkCluster Cluster { get; set; } = null!;

        public async Task InitializeAsync(List<LavalinkNodeOptions> nodes, IDiscordClientWrapper wrapper, ILogger? logger) {
            logger?.Info("Starting music module");

            if (logger != null)
                _lavalinkLogger.LogMessage += (sender, e) => LavalinkLoggerOnLogMessage(sender, e, logger);
            
            if (nodes.Count != 0) {
                logger?.Info("Start building music cluster");
                try {
                    var lavalinkClusterOptions = new LavalinkClusterOptions {
                        Nodes = nodes.ToArray(), StayOnline = true, LoadBalacingStrategy = LoadBalancingStrategy
                    };
                    Cluster = new LavalinkCluster(lavalinkClusterOptions, wrapper, _lavalinkLogger);
                    Cluster.PlayerMoved += ClusterOnPlayerMoved;

                    logger?.Info("Trying to connect to nodes");
                    await Cluster.InitializeAsync();
                    logger?.Info("Cluster  initialized");

                    logger?.Info("Initializing InactivityTrackingService instance with default options");
                    var inactivityTrackingService = new InactivityTrackingService(Cluster, wrapper,
                        new InactivityTrackingOptions {
                            TrackInactivity = true,
                            DisconnectDelay = TimeSpan.FromSeconds(60),
                            PollInterval = TimeSpan.FromSeconds(4)
                        }, _lavalinkLogger);
                    inactivityTrackingService.InactivePlayer += (sender, args) => {
                        if (args.Player is AdvancedLavalinkPlayer embedPlaybackPlayer) {
                            embedPlaybackPlayer.ExecuteShutdown(new EntryLocalized("Music.NoListenersLeft"),
                                new PlayerShutdownParameters {NeedSave = false});
                        }

                        return Task.CompletedTask;
                    };
                }
                catch (Exception e) {
                    logger?.Fatal(e, "Exception while initializing music cluster");
                    Cluster = null!;
                }
            }
            else {
                logger?.Warn("Nodes not found, music disabled!");
            }
        }

        public async Task<FinalLavalinkPlayer> ProvidePlayer(ulong guildId, ulong voiceChannelId, bool recreate = false) {
            var oldPlayer = PlaybackPlayers.FirstOrDefault(playbackPlayer => playbackPlayer.GuildId == guildId);
            if (oldPlayer != null) {
                if (!recreate) return oldPlayer;
                if (!oldPlayer.IsShutdowned) {
                    await oldPlayer.ExecuteShutdown(new PlayerShutdownParameters());
                }

                PlaybackPlayers.Remove(oldPlayer);
            }

            var player = await Cluster.JoinAsync(() => new FinalLavalinkPlayer(this), guildId, voiceChannelId);
            player.Shutdown.Subscribe(entry => { PlaybackPlayers.Remove(player); });
            await player.NodeChanged();
            PlaybackPlayers.Add(player);
            return player;
        }

        public FinalLavalinkPlayer? GetPlayer(ulong guildId) {
            var embedPlaybackPlayer = PlaybackPlayers.FirstOrDefault(player => player.GuildId == guildId);
            return embedPlaybackPlayer?.IsShutdowned == true ? null : embedPlaybackPlayer;
        }

        public Task<IEnumerable<MusicResolverService.MusicResolver>> ResolveQueries(IEnumerable<string> queries) {
            return Task.FromResult(queries.Select(s => _musicResolverService.GetResolver(s, Cluster)));
        }

        private static void LavalinkLoggerOnLogMessage(object? sender, LogMessageEventArgs e, ILogger logger) {
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

        private static LavalinkClusterNode
            LoadBalancingStrategy(LavalinkCluster cluster, IReadOnlyCollection<LavalinkClusterNode> nodes, NodeRequestType type) {
            switch (type) {
                case NodeRequestType.Backup:
                    return LoadBalancingStrategies.LoadStrategy(cluster, nodes, type);
                case NodeRequestType.LoadTrack:
                    var targetNode = nodes.Where(node => node.IsConnected).FirstOrDefault(node => node.Label!.Contains("[RU]"));
                    if (targetNode != null)
                        return targetNode;
                    goto default;
                default:
                    return LoadBalancingStrategies.RoundRobinStrategy(cluster, nodes, type);
            }
        }

        private static Task ClusterOnPlayerMoved(object sender, PlayerMovedEventArgs args) {
            var player = args.Player as AdvancedLavalinkPlayer;
            if (args.CouldBeMoved) {
                player?.WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.PlayerMoved")));
                player?.NodeChanged(args.TargetNode);
            }
            else {
                player?.ExecuteShutdown(new EntryLocalized("Music.PlayerDropped"), new PlayerShutdownParameters());
            }

            return Task.CompletedTask;
        }
        
        public static string EscapeTrack(string track) {
            track = track.Replace("'", "");
            track = track.Replace("\"", "");
            track = track.Replace("#", "");
            return track;
        }
        
        public static List<string> GetMusicQueries(IUserMessage message, string query) {
            return message.Attachments.Select(attachment => attachment.Url)
                          .Concat(query.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())).ToList();
        }
    }
}