using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Common.Music.Tracks;
using Lavalink4NET;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

namespace Common.Music.Resolvers;

public class MusicResolverService(IEnumerable<IMusicResolver> musicResolvers, IAudioService cluster)
{
    public async Task<TrackLoadResult> ResolveTracks(LavalinkApiResolutionScope resolutionScope, string query)
    {
        foreach (var resolver in musicResolvers)
        {
            var canResolve = resolver.CanResolve(query);
            if (!canResolve)
            {
                continue;
            }

            if (!resolver.IsAvailable)
            {
                return TrackLoadResult.CreateError(new TrackException(ExceptionSeverity.Fault,
                    "Target resolving service unavailable", null));
            }

            return await resolver.Resolve(cluster, resolutionScope, query);
        }

        throw new InvalidOperationException("No resolvers to fulfil request");
    }

    public ValueTask<IEncodedTrack[]> EncodeTracks(IEnumerable<IEnlivenQueueItem> tracks) =>
        EncodeTracks(tracks.Select(item => item.Track));

    public ValueTask<IEncodedTrack[]> EncodeTracks(IEnumerable<LavalinkTrack> tracks)
    {
        return tracks
            .Select(EncodeTrack)
            .WhenAll();
    }


    public ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track)
    {
        foreach (var resolver in musicResolvers)
        {
            if (resolver.CanEncodeTrack(track))
            {
                return resolver.EncodeTrack(track);
            }
        }

        throw new InvalidOperationException("No resolvers to fulfil request");
    }

    public ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(IEnumerable<IEncodedTrack> track)
    {
        return track
            .GroupBy(encodedTrack => musicResolvers.First(resolver => resolver.CanDecodeTrack(encodedTrack)))
            .Select(tracks => tracks.Key.DecodeTracks(tracks.ToArray()))
            .WhenAll()
            .PipeAsync(lists =>
                (IReadOnlyList<LavalinkTrack>)lists
                    .SelectMany(list => list)
                    .ToImmutableArray());
    }
}