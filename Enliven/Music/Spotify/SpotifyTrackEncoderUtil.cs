using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using Common.Music.Controller;
using Common.Music.Encoders;
using Lavalink4NET.Player;

namespace Bot.Music.Spotify;

public class SpotifyTrackEncoderUtil : ITrackEncoderUtil {
    private SpotifyMusicResolver _resolver;

    public SpotifyTrackEncoderUtil(SpotifyMusicResolver resolver) {
        _resolver = resolver;
    }
    public IMusicController MusicController { get; set; } = null!;

    public int Priority { get; } = 11;
    public int EncoderId { get; } = 2;

    public Task<bool> CanEncode(LavalinkTrack track) {
        return Task.FromResult(track is SpotifyLavalinkTrack);
    }

    public Task<byte[]> Encode(LavalinkTrack track) {
        var spotifyLavalinkTrack = ((SpotifyLavalinkTrack)track)!;
        return Task.FromResult(Encoding.ASCII.GetBytes(spotifyLavalinkTrack.RelatedSpotifyTrackWrapper.Id));
    }

    public async Task<LavalinkTrack> Decode(byte[] data) {
        var cluster = await MusicController.ClusterTask;
        var spotifyUrl = new SpotifyUrl(Encoding.ASCII.GetString(data), SpotifyUrl.SpotifyUrlType.Track);
        return await _resolver.Resolve(spotifyUrl, cluster).PipeAsync(list => list.First());
    }
}