using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Player;

namespace Common.Music.Encoders {
    public class TrackEncoder {
        private Dictionary<int, ITrackEncoder> _trackEncoders;

        public TrackEncoder(IEnumerable<ITrackEncoder> encoders) {
            _trackEncoders = encoders.OrderByDescending(encoder => encoder.Priority).ToDictionary(encoder => encoder.EncoderId);
        }

        public async Task<EncodedTrack> Encode(LavalinkTrack track) {
            foreach (var (encoderId, encoder) in _trackEncoders) {
                if (await encoder.CanEncode(track)) {
                    return new EncodedTrack(encoderId, await encoder.Encode(track));
                }
            }

            throw new Exception("No suitable encoder registered for this track");
        }

        public async Task<LavalinkTrack> Decode(EncodedTrack track) {
            if (_trackEncoders.TryGetValue(track.EncoderId, out var encoder)) {
                return await encoder.Decode(track.Data);
            }

            throw new Exception("No target registered for this track");
        }
    }
}