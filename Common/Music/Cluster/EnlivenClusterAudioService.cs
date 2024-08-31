using System;
using System.Collections.Immutable;
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
using Lavalink4NET.Events.Players;
using Lavalink4NET.Integrations;
using Lavalink4NET.Players;
using Lavalink4NET.Rest;
using Lavalink4NET.Socket;
using Lavalink4NET.Tracks;
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
        players.PlayerStateChanged += OnPlayerStateChanged;
    }

    public static PlayerFactory<EnlivenLavalinkPlayer, PlaylistLavalinkPlayerOptions> EnlivenPlayerFactory { get; } =
        PlayerFactory.Create<EnlivenLavalinkPlayer, PlaylistLavalinkPlayerOptions>(static properties =>
            new EnlivenLavalinkPlayer(properties));

    public async Task ShutdownPlayer(AdvancedLavalinkPlayer player, PlayerShutdownParameters shutdownParameters,
        IEntry shutdownReason)
    {
        var snapshot = await (player as IPlayerShutdownInternally).ShutdownInternal();
        StoredPlaylist? storedPlaylist = null;
        if (shutdownParameters.SavePlaylist)
        {
            var encodedTracks = await _musicResolverService.EncodeTracks(snapshot.Playlist);
            var trackIndex = snapshot.LastTrack is not null ? snapshot.Playlist.IndexOf(snapshot.LastTrack) : -1;
            var enlivenPlaylist = new EnlivenPlaylist()
            {
                Tracks = encodedTracks,
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
                _logger.LogInformation("Waiting 2 seconds and restarting player");
                await Task.Delay(2000);

                var playlistLavalinkPlayerOptions = ConvertSnapshotToOptions(snapshot);
                var optionsWrapper = new OptionsWrapper<PlaylistLavalinkPlayerOptions>(playlistLavalinkPlayerOptions);
                var retrieveResult = await Players.RetrieveAsync(snapshot.GuildId, snapshot.LastVoiceChannelId,
                    EnlivenPlayerFactory, optionsWrapper);
                if (!retrieveResult.IsSuccess)
                {
                    _logger.LogError("Failed to retrieve player white restarting due to {Status}",
                        retrieveResult.Status);
                }

                var newPlayer = retrieveResult.Player!;
                foreach (var playerDisplay in playerDisplays)
                    await playerDisplay.ChangePlayer(newPlayer);

                newPlayer.WriteToQueueHistory(player.QueueHistory.AsEnumerable());
                newPlayer.WriteToQueueHistory(ReconnectedAfterDisposeEntry.WithArg(storedPlaylist!.Id));
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

    public async ValueTask WaitForAnyNodeAvailable()
    {
        if (Nodes.Any(node => node.Status == LavalinkNodeStatus.Available)) return;

        await Nodes
            .Select(node => node.StartAsync())
            .WhenAll();

        await Task.WhenAny(Nodes.Select(node => node.WaitForReadyAsync().AsTask()));
    }

    private static PlaylistLavalinkPlayerOptions ConvertSnapshotToOptions(PlayerSnapshot snapshot)
    {
        var lastTrack = snapshot.LastTrack;
        if (lastTrack is not null && snapshot.TrackPosition is not null)
        {
            lastTrack.WithStartPosition(snapshot.TrackPosition.Value);
        }

        var playlistLavalinkPlayerOptions = new PlaylistLavalinkPlayerOptions()
        {
            Playlist = snapshot.Playlist,
            InitialTrack = lastTrack,
            LoopingState = snapshot.LoopingState,
            PlayerEffects = snapshot.Effects,
            InitialVolume = snapshot.Volume
        };
        return playlistLavalinkPlayerOptions;
    }
    
    private async Task OnPlayerStateChanged(object sender, PlayerStateChangedEventArgs eventargs)
    {
        if (eventargs.Player is WrappedLavalinkPlayer wrappedLavalinkPlayer)
        {
            await wrappedLavalinkPlayer.NotifyStateChangedAsync(eventargs.State, CancellationToken.None);
        }
    }
}