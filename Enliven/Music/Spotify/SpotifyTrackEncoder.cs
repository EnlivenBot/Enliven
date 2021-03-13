using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Music.Encoders;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;

namespace Bot.Music.Spotify {
    public class SpotifyTrackEncoder : ITrackEncoder {
        private SpotifyMusicResolver _resolver;
        private LavalinkCluster _lavalinkCluster;

        public SpotifyTrackEncoder(SpotifyMusicResolver resolver, LavalinkCluster lavalinkCluster) {
            _lavalinkCluster = lavalinkCluster;
            _resolver = resolver;
        }

        public int Priority { get; } = 11;
        public int EncoderId { get; } = 2;

        public Task<bool> CanEncode(LavalinkTrack track) {
            return Task.FromResult(track is SpotifyLavalinkTrack);
        }

        public Task<byte[]> Encode(LavalinkTrack track) {
            var spotifyLavalinkTrack = (track as SpotifyLavalinkTrack)!;
            return Task.FromResult(Encoding.ASCII.GetBytes(spotifyLavalinkTrack.RelatedSpotifyTrackWrapper.Id));
        }

        public async Task<LavalinkTrack> Decode(byte[] data) {
            return (await (await _resolver.Resolve(_lavalinkCluster,
                    new SpotifyUrl(Encoding.ASCII.GetString(data), SpotifyUrl.SpotifyUrlType.Track))
                ).Resolve()).First();
        }
    }
}