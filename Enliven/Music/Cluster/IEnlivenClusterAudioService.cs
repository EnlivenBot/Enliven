using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Bot.Music.Players;
using Bot.Music.Players.Options;
using Common.Localization.Entries;
using Common.Music;
using Lavalink4NET.Cluster;
using Lavalink4NET.Cluster.Nodes;
using Lavalink4NET.Players;

namespace Bot.Music.Cluster;

public interface IEnlivenClusterAudioService : IClusterAudioService {
    Task ShutdownPlayer(AdvancedLavalinkPlayer player, PlayerShutdownParameters shutdownParameters,
        IEntry shutdownReason);

    ValueTask WaitForAnyNodeAvailable();

    ILavalinkNode GetPlayerNode(ILavalinkPlayer player);

    Task<EnlivenLavalinkPlayer> MovePlayerAsync(EnlivenLavalinkPlayer player, ILavalinkNode targetNode,
        IEntry reason);

    bool TryGetPlayerLaunchOptionsFromLastRun(ulong guildId,
        [NotNullWhen(true)] out PlaylistLavalinkPlayerOptions? options);
}