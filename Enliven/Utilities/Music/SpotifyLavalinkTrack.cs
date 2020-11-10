using System;
using Lavalink4NET.Player;
using Lavalink4NET.Util;

namespace Bot.Utilities.Music {
    public class SpotifyLavalinkTrack : LavalinkTrack {
        public SpotifyTrackWrapper RelatedSpotifyTrackWrapper;

        public SpotifyLavalinkTrack(SpotifyTrackWrapper relatedSpotifyTrackWrapper, LavalinkTrack track) : this(relatedSpotifyTrackWrapper, track.Identifier, track.Author,
            track.Duration, track.IsLiveStream, track.IsSeekable, track.Source, track.Title, track.TrackIdentifier, track.Provider) { }

        public SpotifyLavalinkTrack(SpotifyTrackWrapper relatedSpotifyTrackWrapper, string identifier, LavalinkTrackInfo info) : this(relatedSpotifyTrackWrapper, identifier, info.Author,
            info.Duration, info.IsLiveStream, info.IsSeekable, info.Source, info.Title, info.TrackIdentifier, StreamProviderUtil.GetStreamProvider(info.Source!)) { }

        public SpotifyLavalinkTrack(SpotifyTrackWrapper relatedSpotifyTrackWrapper, string identifier, string author, TimeSpan duration, bool isLiveStream, bool isSeekable,
                                    string? source, string title, string trackIdentifier, StreamProvider provider) : base(identifier, author, duration,
            isLiveStream, isSeekable, source, title, trackIdentifier, provider) {
            RelatedSpotifyTrackWrapper = relatedSpotifyTrackWrapper;
        }
    }
}