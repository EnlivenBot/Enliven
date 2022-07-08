using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Music.Yandex;
using Common;
using Common.Music.Tracks;
using Lavalink4NET.Payloads.Player;
using Lavalink4NET.Player;
using SpotifyAPI.Web;

namespace Bot.Music.Spotify {
    public class SpotifyLavalinkTrack : LavalinkTrack, ITrackHasArtwork {
        public SpotifyTrackWrapper RelatedSpotifyTrackWrapper;
        private SpotifyClient _spotifyClient;

        public SpotifyLavalinkTrack(SpotifyTrackWrapper relatedSpotifyTrackWrapper, LavalinkTrack track, SpotifyClient spotifyClient)
            : base(track.Identifier, track.Author, track.Duration, track.IsLiveStream, track.IsSeekable, track.Uri, track.SourceName, track.Position, track.Title, track.TrackIdentifier, track.Context, track.Provider) {
            RelatedSpotifyTrackWrapper = relatedSpotifyTrackWrapper;
            _spotifyClient = spotifyClient;
        }

        public async ValueTask<Uri?> GetArtwork() {
            if (_spotifyClient == null) return null;
            var imageUrl = await RelatedSpotifyTrackWrapper.GetFullTrack(_spotifyClient)
                .PipeAsync(track => track.Album.Images.FirstOrDefault())
                .PipeAsync(image => image?.Url);
            return imageUrl?.Pipe(s => new Uri(s));
        }
    }
}