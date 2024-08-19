using System.Threading.Tasks;
using Lavalink4NET.Tracks;

namespace Common.Music.Encoders;

public interface IBatchTrackEncoder
{
    public Task<LavalinkTrack> EnqueueDecode(byte[] data);
    public Task Process();
}