using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common.Config;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Encoders;
using Common.Music.Players;
using Common.Music.Resolvers;
using Discord;
using Lavalink4NET.Cluster;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Events;
using Lavalink4NET.Logging;
using Lavalink4NET.Player;
using Lavalink4NET.Tracking;
using ILogger = NLog.ILogger;

namespace Common.Music.Controller {
    public class MusicController : IMusicController, IDisposable {
        private static readonly List<FinalLavalinkPlayer> PlaybackPlayers = new List<FinalLavalinkPlayer>();
        private static readonly Dictionary<ulong, PlayerShutdownParameters> PlayerShutdownParametersMap = new Dictionary<ulong, PlayerShutdownParameters>();
        private static EventLogger _lavalinkLogger = new EventLogger();
        private MusicResolverService _musicResolverService;
        private IGuildConfigProvider _guildConfigProvider;
        private IPlaylistProvider _playlistProvider;
        private EnlivenShardedClient _discordShardedClient;
        private ILogger _logger;
        private List<LavalinkNodeInfo> _lavalinkNodeInfos;
        private TrackEncoder _trackEncoder;

        public MusicController(MusicResolverService musicResolverService, IGuildConfigProvider guildConfigProvider, 
                               IPlaylistProvider playlistProvider, TrackEncoder trackEncoder,
                               EnlivenShardedClient discordShardedClient, ILogger logger, List<LavalinkNodeInfo> lavalinkNodeInfos) {
            _trackEncoder = trackEncoder;
            _lavalinkNodeInfos = lavalinkNodeInfos;
            _logger = logger;
            _discordShardedClient = discordShardedClient;
            _playlistProvider = playlistProvider;
            _guildConfigProvider = guildConfigProvider;
            _musicResolverService = musicResolverService;
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => { Dispose(); };
        }

        public void Dispose() {
            Task.WaitAll(PlaybackPlayers.ToList()
                                        .Select(player => player.ExecuteShutdown(new PlayerShutdownParameters())).ToArray());
        }

        public bool IsMusicEnabled { get; set; }
        public EnlivenLavalinkCluster Cluster { get; set; } = null!;

        public async Task OnPostDiscordStartInitialize() {
            var nodes = _lavalinkNodeInfos.Select(info => info.ToOptions()).ToList();
            var wrapper = new DiscordClientWrapper(_discordShardedClient);
            _logger.Info("Starting music module");

            _lavalinkLogger.LogMessage += (sender, e) => LavalinkLoggerOnLogMessage(e, _logger);

            IsMusicEnabled = nodes.Count != 0;
            if (IsMusicEnabled) {
                await _discordShardedClient.Ready;
                _logger.Info("Start building music cluster");
                try {
                    var lavalinkClusterOptions = new CustomLavalinkClusterOptions<EnlivenLavalinkClusterNode>(
                        (options, clientWrapper, arg3, arg4) => new EnlivenLavalinkClusterNode(options, clientWrapper, arg3, arg4)
                    ) {
                        Nodes = nodes.ToArray(), StayOnline = true, LoadBalacingStrategy = LoadBalancingStrategy
                    };
                    Cluster = new EnlivenLavalinkCluster(lavalinkClusterOptions, wrapper, _lavalinkLogger);
                    Cluster.PlayerMoved += ClusterOnPlayerMoved;

                    _logger.Info("Trying to connect to nodes");
                    await Cluster.InitializeAsync();
                    _logger.Info("Cluster  initialized");

                    _logger.Info("Initializing InactivityTrackingService instance with default options");
                    var inactivityTrackingService = new InactivityTrackingService(Cluster, wrapper,
                        new InactivityTrackingOptions {
                            TrackInactivity = true,
                            DisconnectDelay = TimeSpan.FromSeconds(60),
                            PollInterval = TimeSpan.FromSeconds(4)
                        }, _lavalinkLogger);
                    inactivityTrackingService.InactivePlayer += async (sender, args) => {
                        if (args.Player is AdvancedLavalinkPlayer embedPlaybackPlayer) {
                            await embedPlaybackPlayer.ExecuteShutdown(new EntryLocalized("Music.NoListenersLeft"),
                                new PlayerShutdownParameters {NeedSave = false});
                        }
                    };
                }
                catch (Exception e) {
                    _logger.Fatal(e, "Exception while initializing music cluster");
                    Cluster = null!;
                }
            }
            else {
                _logger.Warn("Nodes not found, music disabled!");
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

            var finalLavalinkPlayer = Cluster.GetPlayer<FinalLavalinkPlayer>(guildId);
            if (finalLavalinkPlayer != null) await finalLavalinkPlayer.DestroyAsync();
            
            var player = await Cluster.JoinAsync(() => new FinalLavalinkPlayer(this, _guildConfigProvider, _playlistProvider, _trackEncoder), guildId, voiceChannelId);
            player.Shutdown.Subscribe(entry => { PlaybackPlayers.Remove(player); });
            PlaybackPlayers.Add(player);
            return player;
        }

        public async Task<FinalLavalinkPlayer> CreatePlayer(PlayerShutdownParameters parameters) {
            var newPlayer = await ProvidePlayer(parameters.GuildId, parameters.LastVoiceChannelId, true);
            newPlayer.Playlist.AddRange(parameters.Playlist!);
            await newPlayer.PlayAsync(parameters.LastTrack!, parameters.TrackPosition);
            if (parameters.PlayerState == PlayerState.Paused) await newPlayer.PauseAsync();
            newPlayer.LoopingState = parameters.LoopingState;
            newPlayer.UpdateCurrentTrackIndex();

            return newPlayer;
        }

        public void StoreShutdownParameters(PlayerShutdownParameters parameters) {
            PlayerShutdownParametersMap[parameters.GuildId] = parameters;
        }

        public async Task<FinalLavalinkPlayer?> RestoreLastPlayer(ulong guildId) {
            var finalLavalinkPlayer = GetPlayer(guildId);
            if (finalLavalinkPlayer != null) return finalLavalinkPlayer;
            if (!PlayerShutdownParametersMap.ContainsKey(guildId)) return null;
            return await CreatePlayer(PlayerShutdownParametersMap[guildId]);
        }

        public FinalLavalinkPlayer? GetPlayer(ulong guildId) {
            var embedPlaybackPlayer = PlaybackPlayers.FirstOrDefault(player => player.GuildId == guildId);
            return embedPlaybackPlayer?.IsShutdowned == true ? null : embedPlaybackPlayer;
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
                    return (EnlivenLavalinkClusterNode) LoadBalancingStrategies.LoadStrategy(cluster, enlivenLavalinkClusterNodes, type);
                case NodeRequestType.LoadTrack:
                    var targetNode = enlivenLavalinkClusterNodes.FirstOrDefault(node => node.IsConnected);
                    if (targetNode != null)
                        return targetNode;
                    goto default;
                default:
                    return (EnlivenLavalinkClusterNode) LoadBalancingStrategies.RoundRobinStrategy(cluster, enlivenLavalinkClusterNodes, type);
            }
        }

        private static Task ClusterOnPlayerMoved(object sender, PlayerMovedEventArgs args) {
            var player = args.Player as AdvancedLavalinkPlayer;
            if (args.CouldBeMoved) {
                player?.WriteToQueueHistory(new HistoryEntry(new EntryLocalized("Music.PlayerMoved")));
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
            var list = new List<string>();
            list.AddRange(ParseByLines(query));
            if (message.Attachments.Count != 0 && message.Attachments.First().Filename == "message.txt") {
                using WebClient webClient = new WebClient();
                list.AddRange(ParseByLines(webClient.DownloadString(message.Attachments.First().Url)));
            }
            else {
                list.AddRange(message.Attachments.Select(attachment => attachment.Url));
            }

            return list;

            IEnumerable<string> ParseByLines(string query1) {
                return query1.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim());
            }
        }
    }
}