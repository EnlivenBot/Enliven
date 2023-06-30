using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Player;
using NLog;

namespace Common.Music.Encoders {
    public class TrackEncoderUtils {
        private ILogger _logger;
        private Dictionary<int, ITrackEncoderUtil> _trackEncoders;

        public TrackEncoderUtils(IEnumerable<ITrackEncoderUtil> encoders, ILogger logger) {
            _logger = logger;
            _trackEncoders = encoders.OrderByDescending(encoder => encoder.Priority).ToDictionary(encoder => encoder.EncoderId);
        }

        public async Task<EncodedTrack> Encode(LavalinkTrack track) {
            foreach (var (encoderId, encoder) in _trackEncoders) {
                if (await encoder.CanEncode(track)) {
                    return new EncodedTrack(encoderId, await encoder.Encode(track));
                }
            }

            _logger.Error("No suitable encoder registered for this track: {track}", track);
            throw new Exception("No suitable encoder registered for this track");
        }

        public async Task<LavalinkTrack> Decode(EncodedTrack track) {
            if (_trackEncoders.TryGetValue(track.EncoderId, out var encoder)) {
                return await encoder.Decode(track.Data);
            }

            _logger.Error("No encoder with {id} id registered", track.EncoderId);
            throw new Exception($"No encoder with {track.EncoderId} id registered");
        }

        public async Task<IEnumerable<LavalinkTrack>> BatchDecode(IEnumerable<EncodedTrack> tracks) {
            HashSet<IBatchTrackEncoder> usedBatchTrackEncoders = new HashSet<IBatchTrackEncoder>();
            var trackTasks = tracks.Select(track => {
                if (_trackEncoders.TryGetValue(track.EncoderId, out var encoder)) {
                    if (!(encoder is IBatchTrackEncoder batchTrackEncoder)) return encoder.Decode(track.Data);
                    usedBatchTrackEncoders.Add(batchTrackEncoder);
                    return batchTrackEncoder.EnqueueDecode(track.Data);
                }

                _logger.Error("No encoder with {id} id registered", track.EncoderId);
                throw new Exception($"No encoder with {track.EncoderId} id registered");
            }).ToList();
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            var enumerable = usedBatchTrackEncoders.Select(encoder => encoder.Process()).ToList();
            return await Task.WhenAll(trackTasks);
        }
    }
}