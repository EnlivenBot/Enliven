using System.Collections.Immutable;
using Common;
using Enliven.MusicResolvers.Base;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

namespace Enliven.MusicResolvers.Lavalink;

public sealed class LavalinkMusicResolver : MusicResolverBase<LavalinkTrack, LavalinkTrackData> {
    public override bool IsAvailable => true;

    public override bool CanResolve(string query) {
        return true;
    }

    public override async ValueTask<MusicResolveResult> Resolve(ITrackManager cluster,
        LavalinkApiResolutionScope resolutionScope, string query, CancellationToken cancellationToken) {
        if (Utilities.IsValidUrl(query))
            return await cluster.LoadTracksAsync(query, TrackSearchMode.None, resolutionScope, cancellationToken);

        var track = await cluster.LoadTrackAsync(query, TrackSearchMode.YouTube, resolutionScope, cancellationToken);
        return track is not null
            ? TrackLoadResult.CreateTrack(track)
            : TrackLoadResult.CreateEmpty();
    }

    protected override ValueTask<IEncodedTrack> EncodeTrackInternal(LavalinkTrack track) {
        return new ValueTask<IEncodedTrack>(new LavalinkTrackData(track.ToString()));
    }

    public override ValueTask<IReadOnlyList<LavalinkTrack>>
        DecodeTracksInternal(IEnumerable<LavalinkTrackData> tracks) {
        return new ValueTask<IReadOnlyList<LavalinkTrack>>(tracks
            .Select(data => LavalinkTrack.Parse(data.StringTrackData, null))
            .ToImmutableArray());
    }
}