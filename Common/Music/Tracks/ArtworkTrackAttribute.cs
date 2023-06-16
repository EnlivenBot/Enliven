using System;
using System.Threading.Tasks;
using Lavalink4NET.Artwork;
using Lavalink4NET.Player;

namespace Common.Music.Tracks {
    public class ArtworkTrackAttribute : ILavalinkTrackAttribute {
        public ArtworkTrackAttribute(Uri? artworkUri) {
            ArtworkUri = artworkUri;
        }
        public Uri? ArtworkUri { get; init; }
    }

    public static class ArtworkTrackAttributeExtensions {
        public static async ValueTask<Uri?> ResolveArtwork(this LavalinkTrack track, IArtworkService artworkService) {
            return await track.GetOrAddAttribute(TrackArtworkResolver)
                .PipeAsync(attribute => attribute.ArtworkUri);
            
            async Task<ArtworkTrackAttribute> TrackArtworkResolver() {
                var artwork = track is ITrackHasArtwork trackHasArtwork
                    ? await trackHasArtwork.GetArtwork()
                    : await artworkService.ResolveAsync(track);
                return new ArtworkTrackAttribute(artwork);
            }
        }
    }
}