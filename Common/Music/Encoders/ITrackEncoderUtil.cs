using System.Threading.Tasks;
using Lavalink4NET.Tracks;

namespace Common.Music.Encoders;

public interface ITrackEncoderUtil
{
    public int Priority { get; }
    public int EncoderId { get; }
    public Task<bool> CanEncode(LavalinkTrack track);
    public Task<byte[]> Encode(LavalinkTrack track);
    public Task<LavalinkTrack> Decode(byte[] data);
}