using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Music.Encoders;
using Lavalink4NET.Player;

namespace Bot.Music.Yandex {
    public class YandexTrackEncoderUtil : ITrackEncoderUtil, IBatchTrackEncoder {
        private YandexClientResolver _clientResolver;

        private ConcurrentDictionary<string, TaskCompletionSource<LavalinkTrack>> _enqueueCache = new ConcurrentDictionary<string, TaskCompletionSource<LavalinkTrack>>();

        public YandexTrackEncoderUtil(YandexClientResolver clientResolver) {
            _clientResolver = clientResolver;
        }
        public Task<LavalinkTrack> EnqueueDecode(byte[] data) {
            var id = Encoding.ASCII.GetString(data);
            var taskCompletionSource = new TaskCompletionSource<LavalinkTrack>();
            return _enqueueCache.GetOrAdd(id, taskCompletionSource).Task;
        }

        public async Task Process() {
            var taskCompletionSources = _enqueueCache.ToDictionary(pair => pair.Key, pair => pair.Value);
            _enqueueCache.Clear();
            var yandexMusicMainResolver = await _clientResolver.GetClient();
            var tracks = await yandexMusicMainResolver.TrackLoader.LoadTracks(taskCompletionSources.Keys);
            foreach (var yandexMusicTrack in tracks) {
                taskCompletionSources[yandexMusicTrack.Id].SetResult(YandexLavalinkTrack.CreateInstance(yandexMusicTrack, yandexMusicMainResolver.DirectUrlLoader));
            }
        }

        public int Priority { get; } = 10;
        public int EncoderId { get; } = 1;

        public Task<bool> CanEncode(LavalinkTrack track) {
            return Task.FromResult(track is YandexLavalinkTrack);
        }

        public Task<byte[]> Encode(LavalinkTrack track) {
            var yandexLavalinkTrack = ((YandexLavalinkTrack)track)!;
            return Task.FromResult(Encoding.ASCII.GetBytes(yandexLavalinkTrack.RelatedYandexTrack.Id));
        }

        public async Task<LavalinkTrack> Decode(byte[] data) {
            var yandexMusicMainResolver = await _clientResolver.GetClient();
            var yandexTrack = await yandexMusicMainResolver.TrackLoader.LoadTrack(Encoding.ASCII.GetString(data));
            return YandexLavalinkTrack.CreateInstance(yandexTrack!, yandexMusicMainResolver.DirectUrlLoader);
        }
    }
}