using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Autofac;
using Common.Config;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Encoders;
using Common.Music.Players;
using Common.Music.Resolvers;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Events;
using Lavalink4NET.Logging;
using Lavalink4NET.Player;
using Lavalink4NET.Tracking;
using ILogger = NLog.ILogger;

namespace Common.Music.Controller {
    public class MusicController : IMusicController {
        private readonly List<FinalLavalinkPlayer> _playbackPlayers = new();
        private readonly Dictionary<ulong, PlayerSnapshot> _playerShutdownParametersMap = new();
        private readonly EventLogger _lavalinkLogger = new();
        private readonly MusicResolverService _musicResolverService;
        private readonly IGuildConfigProvider _guildConfigProvider;
        private readonly IPlaylistProvider _playlistProvider;
        private readonly EnlivenShardedClient _discordShardedClient;
        private readonly ILogger _logger;
        private readonly ILifetimeScope _lifetimeScope;
        private readonly IEnumerable<LavalinkNodeInfo> _lavalinkNodeInfos;
        private readonly TrackEncoder _trackEncoder;

        public MusicController(MusicResolverService musicResolverService, IGuildConfigProvider guildConfigProvider,
                               IPlaylistProvider playlistProvider, TrackEncoder trackEncoder,
                               EnlivenShardedClient discordShardedClient, ILogger logger,
                               InstanceConfig instanceConfig, GlobalConfig globalConfig,
                               ILifetimeScope lifetimeScope) {
            _trackEncoder = trackEncoder;
            _lavalinkNodeInfos = globalConfig.LavalinkNodes.Concat(instanceConfig.LavalinkNodes).Distinct();
            _logger = logger;
            _lifetimeScope = lifetimeScope;
            _discordShardedClient = discordShardedClient;
            _playlistProvider = playlistProvider;
            _guildConfigProvider = guildConfigProvider;
            _musicResolverService = musicResolverService;
        }

        public Task OnShutdown(bool isDiscordStarted) {
            var tasks = _playbackPlayers
                .ToList()
                .Select(player => player.Shutdown(new PlayerShutdownParameters()))
                .ToArray();
            return Task.WhenAll(tasks);
        }

        public bool IsMusicEnabled { get; private set; }

        private Task<EnlivenLavalinkCluster>? _clusterTask;
        public Task<EnlivenLavalinkCluster> ClusterTask => _clusterTask ?? throw new InvalidOperationException("This task initialized after Ready event in DiscordClient");
        protected EnlivenLavalinkCluster Cluster => ClusterTask.GetAwaiter().GetResult();

        public Task OnDiscordReady() {
            _clusterTask = InitializeCluster();
            return _clusterTask;
        }
        private async Task<EnlivenLavalinkCluster> InitializeCluster() {
            _lavalinkLogger.LogMessage += (sender, e) => LavalinkLoggerOnLogMessage(e, _logger);

            var nodes = _lavalinkNodeInfos.Select(ConvertNodeInfoToOptions).ToList();
            var wrapper = new DiscordClientWrapper(_discordShardedClient);

            IsMusicEnabled = nodes.Count != 0;
            if (IsMusicEnabled) {
                _logger.Info("Start building music cluster");
                try {
                    EnlivenLavalinkClusterNode NodeFactory(LavalinkNodeOptions options, IDiscordClientWrapper clientWrapper, Lavalink4NET.Logging.ILogger? arg3, ILavalinkCache? arg4)
                        => new EnlivenLavalinkClusterNode(options, clientWrapper, arg3, arg4);

                    var lavalinkClusterOptions = new CustomLavalinkClusterOptions<EnlivenLavalinkClusterNode>(NodeFactory) {
                        Nodes = nodes.ToArray(), StayOnline = true, LoadBalacingStrategy = LoadBalancingStrategy
                    };
                    var cluster = new EnlivenLavalinkCluster(lavalinkClusterOptions, wrapper, _lavalinkLogger);
                    cluster.PlayerMoved += ClusterOnPlayerMoved;

                    _logger.Info("Trying to connect to nodes");
                    await cluster.InitializeAsync();
                    _logger.Info("Cluster initialized");

                    _logger.Info("Initializing InactivityTrackingService instance with default options");
                    var inactivityTrackingOptions = new InactivityTrackingOptions {
                        TrackInactivity = true,
                        DisconnectDelay = TimeSpan.FromSeconds(60),
                        PollInterval = TimeSpan.FromSeconds(4)
                    };
                    var inactivityTrackingService = new InactivityTrackingService(cluster, wrapper, inactivityTrackingOptions, _lavalinkLogger);
                    inactivityTrackingService.InactivePlayer += InactivityTracking_OnInactivePlayer;
                    return cluster;
                }
                catch (Exception e) {
                    _logger.Fatal(e, "Exception while initializing music cluster");
                    await _lifetimeScope.DisposeAsync();
                    throw new Exception("MusicController failed to initialize");
                }
            }
            else {
                _logger.Warn("Nodes not found, music disabled!");
            }
            return null!;
        }

        private async Task InactivityTracking_OnInactivePlayer(object sender, InactivePlayerEventArgs args) {
            if (args.Player is AdvancedLavalinkPlayer embedPlaybackPlayer) {
                await embedPlaybackPlayer.Shutdown(new EntryLocalized("Music.NoListenersLeft"), new PlayerShutdownParameters {
                    SavePlaylist = false
                });
            }
        }

        private int _nodeIdCounter;
        private LavalinkNodeOptions ConvertNodeInfoToOptions(LavalinkNodeInfo info) {
            var label = info.Name;
            if (string.IsNullOrWhiteSpace(label)) {
                label = $"Node №{++_nodeIdCounter}";
                _logger.Info("Name {NodeName} assingned to node with url {NodeUrl} ({NodeWsUrl})", label, info.RestUri, info.WebSocketUri);
            }

            return new LavalinkNodeOptions {
                RestUri = info.RestUri,
                WebSocketUri = info.WebSocketUri,
                Password = info.Password,
                DisconnectOnStop = false,
                Label = label
            };
        }

        public async Task<FinalLavalinkPlayer> ProvidePlayer(ulong guildId, ulong voiceChannelId, bool recreate = false) {
            var oldPlayer = _playbackPlayers.FirstOrDefault(playbackPlayer => playbackPlayer.GuildId == guildId);
            if (oldPlayer != null) {
                if (!recreate) return oldPlayer;
                if (!oldPlayer.IsShutdowned) {
                    await oldPlayer.Shutdown(new PlayerShutdownParameters());
                }

                _playbackPlayers.Remove(oldPlayer);
            }

            var finalLavalinkPlayer = Cluster.GetPlayer<FinalLavalinkPlayer>(guildId);
            if (finalLavalinkPlayer != null) await finalLavalinkPlayer.DestroyAsync();

            var player = await Cluster.JoinAsync(PlayerFactory, guildId, voiceChannelId);
            _ = player.ShutdownTask.ContinueWith(_ => _playbackPlayers.Remove(player));
            _playbackPlayers.Add(player);
            return player;
        }

        private FinalLavalinkPlayer PlayerFactory() {
            return new FinalLavalinkPlayer(this, _guildConfigProvider, _playlistProvider, _trackEncoder);
        }

        public void StoreSnapshot(PlayerSnapshot parameters) {
            _playerShutdownParametersMap[parameters.GuildId] = parameters;
        }

        public PlayerSnapshot? GetPlayerLastSnapshot(ulong guildId) {
            return _playerShutdownParametersMap.TryGetValue(guildId, out var playerSnapshot) ? playerSnapshot : null;
        }

        public FinalLavalinkPlayer? GetPlayer(ulong guildId) {
            return _playbackPlayers.FirstOrDefault(player =>
                player.GuildId == guildId
             && !player.IsShutdowned
             && player.State is not (PlayerState.NotConnected or PlayerState.Destroyed));
        }

        public Task<IEnumerable<MusicResolver>> ResolveQueries(IEnumerable<string> queries) {
            return Task.FromResult(queries.Select(s => _musicResolverService.GetResolver(s, Cluster)));
        }

        private static void LavalinkLoggerOnLogMessage(LogMessageEventArgs e, ILogger logger) {
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

        public static EnlivenLavalinkClusterNode LoadBalancingStrategy(LavalinkCluster cluster,
                                                                       IReadOnlyCollection<EnlivenLavalinkClusterNode> enlivenLavalinkClusterNodes,
                                                                       NodeRequestType type) {
            switch (type) {
                case NodeRequestType.Backup:
                    return (EnlivenLavalinkClusterNode)LoadBalancingStrategies.LoadStrategy(cluster, enlivenLavalinkClusterNodes, type);
                case NodeRequestType.LoadTrack:
                    var targetNode = enlivenLavalinkClusterNodes.FirstOrDefault(node => node.IsConnected);
                    if (targetNode != null)
                        return targetNode;
                    goto default;
                default:
                    return (EnlivenLavalinkClusterNode)LoadBalancingStrategies.RoundRobinStrategy(cluster, enlivenLavalinkClusterNodes, type);
            }
        }

        private static Task ClusterOnPlayerMoved(object sender, PlayerMovedEventArgs args) {
            var player = args.Player as AdvancedLavalinkPlayer;
            if (args.CouldBeMoved) {
                player?.WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.PlayerMoved")));
            }
            else {
                player?.Shutdown(new EntryLocalized("Music.PlayerDropped"), new PlayerShutdownParameters());
            }

            return Task.CompletedTask;
        }

        public static string EscapeTrack(string track) {
            track = track.Replace("'", "");
            track = track.Replace("\"", "");
            track = track.Replace("#", "");
            return track;
        }
    }
}