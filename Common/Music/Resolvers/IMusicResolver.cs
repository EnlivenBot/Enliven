using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

namespace Common.Music.Resolvers;

public interface IMusicResolver
{
    bool IsAvailable { get; }
    bool CanResolve(string query);
    ValueTask<MusicResolveResult> Resolve(ITrackManager cluster, LavalinkApiResolutionScope resolutionScope,
        string query, CancellationToken cancellationToken);
    bool CanEncodeTrack(LavalinkTrack track);
    ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track);
    bool CanDecodeTrack(IEncodedTrack track);
    ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(params IEncodedTrack[] tracks);
}