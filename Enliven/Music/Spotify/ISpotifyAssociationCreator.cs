using System.Threading.Tasks;
using Lavalink4NET.Cluster;

namespace Bot.Music.Spotify {
    public interface ISpotifyAssociationCreator {
        Task<SpotifyAssociation?> ResolveAssociation(SpotifyTrackWrapper spotifyTrackWrapper, LavalinkCluster lavalinkCluster);
    }
}