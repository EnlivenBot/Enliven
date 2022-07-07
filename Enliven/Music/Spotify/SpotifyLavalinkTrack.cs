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
            : this(relatedSpotifyTrackWrapper, track.Identifier, track.Author, track.Duration, track.IsLiveStream, track.IsSeekable, track.Source, track.Title, track.TrackIdentifier, track.Provider, spotifyClient) { }

        public SpotifyLavalinkTrack(SpotifyTrackWrapper relatedSpotifyTrackWrapper, string identifier, LavalinkTrackInfo info, SpotifyClient spotifyClient) : this(relatedSpotifyTrackWrapper, identifier, info.Author,
            info.Duration, info.IsLiveStream, info.IsSeekable, info.Source, info.Title, info.TrackIdentifier, StreamProviderUtil.GetStreamProvider(info.Source!), spotifyClient) { }

        public SpotifyLavalinkTrack(SpotifyTrackWrapper relatedSpotifyTrackWrapper, string identifier, string author, TimeSpan duration, bool isLiveStream, bool isSeekable,
                                    string? source, string title, string trackIdentifier, StreamProvider provider, SpotifyClient spotifyClient) : base(identifier, author, duration,
            isLiveStream, isSeekable, source, title, trackIdentifier, provider) {
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