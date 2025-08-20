using Lavalink4NET.Rest;
using Lavalink4NET.Tracks;

namespace Enliven.MusicResolvers.Base;

public interface IMusicResolver {
    bool IsAvailable { get; }
    bool CanResolve(string query);

    ValueTask<MusicResolveResult> Resolve(ITrackManager cluster, LavalinkApiResolutionScope resolutionScope,
        string query, CancellationToken cancellationToken);

    bool CanEncodeTrack(LavalinkTrack track);
    ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track);
    bool CanDecodeTrack(IEncodedTrack track);
    ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(params IEncodedTrack[] tracks);
}