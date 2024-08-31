using System.Threading.Tasks;
using Common.Localization.Entries;
using Common.Music.Players;
using Lavalink4NET.Cluster;

namespace Common.Music.Cluster;

public interface IEnlivenClusterAudioService : IClusterAudioService
{
    public Task ShutdownPlayer(AdvancedLavalinkPlayer player, PlayerShutdownParameters shutdownParameters,
        IEntry shutdownReason);

    ValueTask WaitForAnyNodeAvailable();
}