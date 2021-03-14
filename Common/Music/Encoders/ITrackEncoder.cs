using System.Threading.Tasks;
using Lavalink4NET.Player;

namespace Common.Music.Encoders
{
    public interface ITrackEncoder
    {
        public int Priority { get; }
        public int EncoderId { get; }
        public Task<bool> CanEncode(LavalinkTrack track);
        public Task<byte[]> Encode(LavalinkTrack track);
        public Task<LavalinkTrack> Decode(byte[] data);
    }
}