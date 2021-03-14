using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Music.Encoders;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;
using YandexMusicResolver;

namespace Bot.Music.Yandex {
    public class YandexTrackEncoder : ITrackEncoder, IBatchTrackEncoder {
        private YandexClientResolver _clientResolver;

        public YandexTrackEncoder(YandexClientResolver clientResolver) {
            _clientResolver = clientResolver;
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
            var yandexMusicMainResolver = await _clientResolver.GetClient();
            var yandexTrack = await yandexMusicMainResolver.TrackLoader.LoadTrack(Encoding.ASCII.GetString(data));
            return new YandexLavalinkTrack(yandexTrack!, yandexMusicMainResolver.DirectUrlLoader);
        }

        private ConcurrentDictionary<string, TaskCompletionSource<LavalinkTrack>> _enqueueCache = new ConcurrentDictionary<string, TaskCompletionSource<LavalinkTrack>>();
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
                taskCompletionSources[yandexMusicTrack.Id].SetResult(new YandexLavalinkTrack(yandexMusicTrack, yandexMusicMainResolver.DirectUrlLoader));
            }
        }
    }
}