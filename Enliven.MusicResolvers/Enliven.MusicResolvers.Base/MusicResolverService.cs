using Common;
using Common.Music.Tracks;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;

namespace Enliven.MusicResolvers.Base;

public class MusicResolverService(
    IEnumerable<IMusicResolver> musicResolvers,
    ITrackManager cluster,
    ILogger<MusicResolverService> logger) {
    public async Task<MusicResolveResult> ResolveTracks(LavalinkApiResolutionScope resolutionScope, string query,
        CancellationToken cancellationToken) {
        foreach (var resolver in musicResolvers) {
            var canResolve = resolver.CanResolve(query);
            if (!canResolve) {
                continue;
            }

            if (!resolver.IsAvailable) {
                return new MusicResolveResult(new TrackException(ExceptionSeverity.Fault,
                    "Target resolving service unavailable", null));
            }

            try {
                return await resolver.Resolve(cluster, resolutionScope, query, cancellationToken);
            }
            catch (Exception e) {
                logger.LogError(e, "Error while resolving track {Query}", query);
                return new MusicResolveResult(new TrackException(ExceptionSeverity.Fault,
                    "Exception while resolving track", e.Message));
            }
        }

        throw new InvalidOperationException("No resolvers to fulfil request");
    }

    public ValueTask<IEncodedTrack[]> EncodeTracks(IEnumerable<IEnlivenQueueItem> tracks) =>
        EncodeTracks(tracks.Select(item => item.Track));

    public ValueTask<IEncodedTrack[]> EncodeTracks(IEnumerable<LavalinkTrack> tracks) {
        return tracks
            .Select(EncodeTrack)
            .WhenAll();
    }


    public ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track) {
        foreach (var resolver in musicResolvers) {
            if (resolver.CanEncodeTrack(track)) {
                return resolver.EncodeTrack(track);
            }
        }

        throw new InvalidOperationException("No resolvers to fulfil request");
    }

    public ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(IEnumerable<IEncodedTrack> track) {
        return track
            .GroupBy(encodedTrack => musicResolvers.First(resolver => resolver.CanDecodeTrack(encodedTrack)))
            .Select(tracks => tracks.Key.DecodeTracks(tracks.ToArray()))
            .WhenAll()
            .PipeAsync(lists =>
                (IReadOnlyList<LavalinkTrack>) [
                    ..lists
                        .SelectMany(list => list)
                ]);
    }
}