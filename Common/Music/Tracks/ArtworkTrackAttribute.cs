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
            return await track.GetOrAddAttribute(async () => new ArtworkTrackAttribute(await artworkService.ResolveAsync(track)))
                .PipeAsync(attribute => attribute.ArtworkUri);
        }
    }
}