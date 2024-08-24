using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

namespace Common.Music.Resolvers;

public abstract class MusicResolverBase<TTrack, TEncodedTrack> : IMusicResolver
    where TTrack : LavalinkTrack
    where TEncodedTrack : class
{
    public abstract bool IsAvailable { get; }
    public abstract bool CanResolve(string query);

    public abstract ValueTask<TrackLoadResult> Resolve(ITrackManager cluster,
        LavalinkApiResolutionScope resolutionScope, string query);

    public bool CanEncodeTrack(LavalinkTrack track)
    {
        if (track is TTrack typedTrack)
        {
            return CanEncodeTrackInternal(typedTrack);
        }

        return false;
    }

    public ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track)
    {
        return EncodeTrackInternal((TTrack)track);
    }

    public bool CanDecodeTrack(IEncodedTrack track)
    {
        if (track is TEncodedTrack typedTrack)
        {
            return CanDecodeTrackInternal(typedTrack);
        }

        return false;
    }

    public ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(params IEncodedTrack[] tracks)
        => DecodeTracksInternal(tracks.Cast<TEncodedTrack>());

    protected virtual bool CanEncodeTrackInternal(TTrack track) => true;

    protected abstract ValueTask<IEncodedTrack> EncodeTrackInternal(TTrack track);

    protected virtual bool CanDecodeTrackInternal(TEncodedTrack track) => true;

    public abstract ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracksInternal(IEnumerable<TEncodedTrack> tracks);
}