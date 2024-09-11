using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Common.Localization.Entries;
using Common.Music.Players;
using Common.Music.Players.Options;
using Lavalink4NET.Cluster;
using Lavalink4NET.Cluster.Nodes;
using Lavalink4NET.Players;

namespace Common.Music.Cluster;

public interface IEnlivenClusterAudioService : IClusterAudioService
{
    public Task ShutdownPlayer(AdvancedLavalinkPlayer player, PlayerShutdownParameters shutdownParameters,
        IEntry shutdownReason);

    ValueTask WaitForAnyNodeAvailable();
    ILavalinkNode GetPlayerNode(ILavalinkPlayer player);
    bool TryGetPlayerLaunchOptionsFromLastRun(ulong guildId, [NotNullWhen(true)] out PlaylistLavalinkPlayerOptions? options);
}