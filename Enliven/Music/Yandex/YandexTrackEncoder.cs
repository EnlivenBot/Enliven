using System.Text;
using System.Threading.Tasks;
using Common.Music.Encoders;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;
using YandexMusicResolver;

namespace Bot.Music.Yandex {
    public class YandexTrackEncoder : ITrackEncoder {
        private YandexMusicMainResolver _resolver;

        public YandexTrackEncoder(YandexMusicMainResolver resolver) {
            _resolver = resolver;
        }

        public int Priority { get; } = 10;
        public int EncoderId { get; } = 1;

        public Task<bool> CanEncode(LavalinkTrack track) {
            return Task.FromResult(track is YandexLavalinkTrack);
        }

        public Task<byte[]> Encode(LavalinkTrack track) {
            var yandexLavalinkTrack = (track as YandexLavalinkTrack)!;
            return Task.FromResult(Encoding.ASCII.GetBytes(yandexLavalinkTrack.RelatedYandexTrack.Id));
        }

        public async Task<LavalinkTrack> Decode(byte[] data) {
            var yandexTrack = await _resolver.TrackLoader.LoadTrack(Encoding.ASCII.GetString(data));
            return new YandexLavalinkTrack(yandexTrack!, _resolver.DirectUrlLoader);
        }
    }
}