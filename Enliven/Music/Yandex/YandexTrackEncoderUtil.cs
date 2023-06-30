using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Music.Encoders;
using Lavalink4NET.Player;
using YandexMusicResolver;

namespace Bot.Music.Yandex;

public class YandexTrackEncoderUtil : ITrackEncoderUtil, IBatchTrackEncoder {
    private readonly IYandexMusicMainResolver _yandexMusicMainResolver;

    private ConcurrentDictionary<long, TaskCompletionSource<LavalinkTrack>> _enqueueCache = new();

    public YandexTrackEncoderUtil(IYandexMusicMainResolver yandexMusicMainResolver) {
        _yandexMusicMainResolver = yandexMusicMainResolver;
    }
    public Task<LavalinkTrack> EnqueueDecode(byte[] data) {
        var id = long.Parse(Encoding.ASCII.GetString(data));
        var taskCompletionSource = new TaskCompletionSource<LavalinkTrack>();
        return _enqueueCache.GetOrAdd(id, taskCompletionSource).Task;
    }

    public async Task Process() {
        var taskCompletionSources = _enqueueCache.ToDictionary(pair => pair.Key, pair => pair.Value);
        _enqueueCache.Clear();
        var tracks = await _yandexMusicMainResolver.TrackLoader.LoadTracks(taskCompletionSources.Keys);
        foreach (var yandexMusicTrack in tracks) taskCompletionSources[yandexMusicTrack.Id].SetResult(YandexLavalinkTrack.CreateInstance(yandexMusicTrack, _yandexMusicMainResolver.DirectUrlLoader));
    }

    public int Priority { get; } = 10;
    public int EncoderId { get; } = 1;

    public Task<bool> CanEncode(LavalinkTrack track) {
        return Task.FromResult(track is YandexLavalinkTrack);
    }

    public Task<byte[]> Encode(LavalinkTrack track) {
        var yandexLavalinkTrack = ((YandexLavalinkTrack)track)!;
        return Task.FromResult(Encoding.ASCII.GetBytes(yandexLavalinkTrack.RelatedYandexTrack.Id.ToString()));
    }

    public async Task<LavalinkTrack> Decode(byte[] data) {
        var id = long.Parse(Encoding.ASCII.GetString(data));
        var yandexTrack = await _yandexMusicMainResolver.TrackLoader.LoadTrack(id);
        return YandexLavalinkTrack.CreateInstance(yandexTrack!, _yandexMusicMainResolver.DirectUrlLoader);
    }
}