using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Lavalink4NET;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

namespace Common.Music.Resolvers.Lavalink;

public sealed class LavalinkMusicResolver : MusicResolverBase<LavalinkTrack, LavalinkTrackData>
{
    public override bool IsAvailable => true;

    public override bool CanResolve(string query)
    {
        return true;
    }

    public override ValueTask<TrackLoadResult> Resolve(IAudioService cluster,
        LavalinkApiResolutionScope resolutionScope, string query)
    {
        var trackSearchMode = Utilities.IsValidUrl(query) ? TrackSearchMode.None : TrackSearchMode.YouTube;
        return cluster.Tracks.LoadTracksAsync(query, trackSearchMode, resolutionScope);
    }

    protected override ValueTask<IEncodedTrack> EncodeTrackInternal(LavalinkTrack track)
    {
        return new ValueTask<IEncodedTrack>(new LavalinkTrackData(track.ToString()));
    }

    public override ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracksInternal(IEnumerable<LavalinkTrackData> tracks)
    {
        return new ValueTask<IReadOnlyList<LavalinkTrack>>(tracks
            .Select(data => LavalinkTrack.Parse(data.StringTrackData, null))
            .ToImmutableArray());
    }
}