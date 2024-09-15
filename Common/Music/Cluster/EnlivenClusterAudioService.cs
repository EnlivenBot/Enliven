using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Config;
using Common.Localization.Entries;
using Common.Music.Players;
using Common.Music.Players.Options;
using Common.Music.Resolvers;
using Lavalink4NET.Clients;
using Lavalink4NET.Cluster;
using Lavalink4NET.Cluster.Nodes;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Integrations;
using Lavalink4NET.Players;
using Lavalink4NET.Rest;
using Lavalink4NET.Socket;
using Lavalink4NET.Tracks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common.Music.Cluster;

public class EnlivenClusterAudioService : ClusterAudioService, IEnlivenClusterAudioService
{
    private static readonly IEntry ConcatLines = new EntryString("{0}\n{1}");
    private static readonly IEntry ResumeViaPlaylists = new EntryLocalized("Music.ResumeViaPlaylists");
    private static readonly IEntry PlaybackStopped = new EntryLocalized("Music.PlaybackStopped");
    private static readonly IEntry TryReconnectAfterDispose = new EntryLocalized("Music.TryReconnectAfterDispose");
    private static readonly IEntry ReconnectedAfterDisposeEntry = new EntryLocalized("Music.ReconnectedAfterDispose");
    private readonly ILogger<EnlivenClusterAudioService> _logger;
    private readonly MusicResolverService _musicResolverService;
    private readonly IPlaylistProvider _playlistProvider;

    private readonly Dictionary<ulong, PlayerSnapshot> _lastPlayerSnapshots = new();

    public EnlivenClusterAudioService(IPlaylistProvider playlistProvider, MusicResolverService musicResolverService,
        IDiscordClientWrapper discordClient, ILavalinkSocketFactory socketFactory,
        ILavalinkApiClientProvider lavalinkApiClientProvider, ILavalinkApiClientFactory lavalinkApiClientFactory,
        IIntegrationManager integrations, IPlayerManager players, ITrackManager tracks,
        IOptions<ClusterAudioServiceOptions> options, ILoggerFactory loggerFactory,
        ILogger<EnlivenClusterAudioService> logger) : base(discordClient, socketFactory,
        lavalinkApiClientProvider, lavalinkApiClientFactory, integrations, players, tracks, options, loggerFactory)
    {
        _playlistProvider = playlistProvider;
        _musicResolverService = musicResolverService;
        _logger = logger;
    }

    public static PlayerFactory<EnlivenLavalinkPlayer, PlaylistLavalinkPlayerOptions> EnlivenPlayerFactory { get; } =
        PlayerFactory.Create<EnlivenLavalinkPlayer, PlaylistLavalinkPlayerOptions>(static properties =>
            new EnlivenLavalinkPlayer(properties));

    public ILavalinkNode GetPlayerNode(ILavalinkPlayer player)
    {
        return Nodes.FirstOrDefault(node => node.SessionId == player.SessionId)
               ?? throw new InvalidOperationException("No node serving this player");
    }

    public async Task ShutdownPlayer(AdvancedLavalinkPlayer player, PlayerShutdownParameters shutdownParameters,
        IEntry shutdownReason)
    {
        var snapshot = await (player as IPlayerShutdownInternally).ShutdownInternal();
        _lastPlayerSnapshots[player.GuildId] = snapshot;

        StoredPlaylist? storedPlaylist = null;
        if (shutdownParameters.SavePlaylist)
        {
            var encodedTracks = await _musicResolverService.EncodeTracks(snapshot.Playlist);
            var byteTracks = encodedTracks.Select(track => MessagePackSerializer.Typeless.Serialize(track)).ToArray();
            var trackIndex = snapshot.LastTrack is not null ? snapshot.Playlist.IndexOf(snapshot.LastTrack) : -1;
            var enlivenPlaylist = new EnlivenPlaylist()
            {
                Tracks = byteTracks,
                TrackPosition = snapshot.TrackPosition,
                TrackIndex = trackIndex
            };
            storedPlaylist = _playlistProvider.StorePlaylist(enlivenPlaylist, UserLink.Current);
            shutdownReason = ConcatLines.WithArg(shutdownReason, ResumeViaPlaylists.WithArg(storedPlaylist.Id));
        }

        var playerDisplays = player.Displays.ToImmutableArray();
        player.Displays.Clear();
        if (shutdownParameters.ShutdownDisplays)
        {
            await playerDisplays
                .Select(ShutdownDisplay)
                .WhenAll();
        }
        else if (shutdownParameters.SavePlaylist)
        {
            await playerDisplays
                .Select(LeaveNotificationToDisplay)
                .WhenAll();
        }

        if (shutdownParameters.RestartPlayer)
        {
            _ = Task.Run(async () =>
            {
                var playlistLavalinkPlayerOptions = ConvertSnapshotToOptions(snapshot);
                var optionsWrapper = new OptionsWrapper<PlaylistLavalinkPlayerOptions>(playlistLavalinkPlayerOptions);
                var playerRetrieveOptions = new PlayerRetrieveOptions { ChannelBehavior = PlayerChannelBehavior.Join };
                var retrieveResult = await Players.RetrieveAsync(snapshot.GuildId, snapshot.LastVoiceChannelId,
                    EnlivenPlayerFactory, optionsWrapper, playerRetrieveOptions);
                if (!retrieveResult.IsSuccess)
                {
                    _logger.LogError("Failed to retrieve player white restarting due to {Status}",
                        retrieveResult.Status);

                    return;
                }

                Debug.Assert(retrieveResult.Player is not null);
                var newPlayer = retrieveResult.Player;
                foreach (var playerDisplay in playerDisplays)
                    await playerDisplay.ChangePlayer(newPlayer);

                newPlayer.WriteToQueueHistory(player.QueueHistory.AsEnumerable());
                var playerNode = GetPlayerNode(newPlayer);
                newPlayer.WriteToQueueHistory(ReconnectedAfterDisposeEntry.WithArg(playerNode.Label));
            });
        }

        return;

        async Task ShutdownDisplay(IPlayerDisplay display)
        {
            try
            {
                await display.ExecuteShutdown(PlaybackStopped, shutdownReason);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while shutdowning {DisplayType}", display.GetType().Name);
            }
        }

        async Task LeaveNotificationToDisplay(IPlayerDisplay display)
        {
            try
            {
                await display.ExecuteShutdown(PlaybackStopped, TryReconnectAfterDispose.WithArg(storedPlaylist!.Id));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while shutdowning {DisplayType}", display.GetType().Name);
            }
        }
    }

    protected override async ValueTask OnConnectionClosedAsync(ConnectionClosedEventArgs eventArgs,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var lavalinkSocketLabel = eventArgs.LavalinkSocket.Label;
        var disconnectedNode = Nodes.First(node => node.Label == lavalinkSocketLabel);
        var disconnectedNodeSessionId = disconnectedNode.SessionId;
        var players = Players.Players
            .Where(player => player.SessionId == disconnectedNodeSessionId)
            .Where(player => player.State != PlayerState.Destroyed)
            .Cast<EnlivenLavalinkPlayer>()
            .ToImmutableArray();

        if (players.Length == 0)
        {
            return;
        }

        var anyNodeAvailable = Nodes.Any(node => node.Status == LavalinkNodeStatus.Available);
        var shutdownReason = anyNodeAvailable
            ? new EntryLocalized("Music.PlayerMoved")
            : new EntryLocalized("Music.PlayerDropped");
        var shutdownParameters = anyNodeAvailable
            ? new PlayerShutdownParameters { RestartPlayer = true, SavePlaylist = false, ShutdownDisplays = false }
            : new PlayerShutdownParameters { RestartPlayer = false, ShutdownDisplays = true, SavePlaylist = true };

        await players
            .Select(player => player.Shutdown(shutdownReason, shutdownParameters))
            .WhenAll();
    }

    public async ValueTask WaitForAnyNodeAvailable()
    {
        if (Nodes.Any(node => node.Status == LavalinkNodeStatus.Available)) return;

        await Nodes
            .Select(node => node.StartAsync())
            .WhenAll();

        await Task.WhenAny(Nodes.Select(node => node.WaitForReadyAsync().AsTask()));
    }

    public bool TryGetPlayerLaunchOptionsFromLastRun(ulong guildId,
        [NotNullWhen(true)] out PlaylistLavalinkPlayerOptions? options)
    {
        options = null;
        if (!_lastPlayerSnapshots.TryGetValue(guildId, out var snapshot)) return false;
        options = ConvertSnapshotToOptions(snapshot);
        return true;
    }

    private static PlaylistLavalinkPlayerOptions ConvertSnapshotToOptions(PlayerSnapshot snapshot)
    {
        var playlistLavalinkPlayerOptions = new PlaylistLavalinkPlayerOptions()
        {
            Playlist = snapshot.Playlist,
            InitialTrack = snapshot.LastTrack,
            InitialPosition = snapshot.TrackPosition,
            LoopingState = snapshot.LoopingState,
            PlayerEffects = snapshot.Effects,
            InitialVolume = snapshot.Volume,
        };
        return playlistLavalinkPlayerOptions;
    }
}