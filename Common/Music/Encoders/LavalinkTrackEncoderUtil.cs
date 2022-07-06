using System;
using System.Threading.Tasks;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;

namespace Common.Music.Encoders
{
    public class LavalinkTrackEncoderUtil : ITrackEncoderUtil
    {
        public int Priority { get; } = int.MinValue;
        public int EncoderId { get; } = 0;

        public Task<bool> CanEncode(LavalinkTrack track)
        {
            return Task.FromResult(true);
        }

        public Task<byte[]> Encode(LavalinkTrack track)
        {
            return Task.FromResult(Convert.FromBase64String(track.Identifier));
        }

        public Task<LavalinkTrack> Decode(byte[] data)
        {
            return Task.FromResult(TrackDecoder.DecodeTrack(Convert.ToBase64String(data)));
        }
    }
}