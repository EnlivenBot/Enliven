using System;
using Bot.Utilities.Music;
using Lavalink4NET.Player;
using Lavalink4NET.Util;

namespace Bot.DiscordRelated.Music.Tracks {
    public class SpotifyLavalinkTrack : LavalinkTrack {
        public SpotifyTrack RelatedSpotifyTrack;

        public SpotifyLavalinkTrack(SpotifyTrack relatedSpotifyTrack, LavalinkTrack track) : this(relatedSpotifyTrack, track.Identifier, track.Author,
            track.Duration, track.IsLiveStream, track.IsSeekable, track.Source, track.Title, track.TrackIdentifier, track.Provider) { }

        public SpotifyLavalinkTrack(SpotifyTrack relatedSpotifyTrack, string identifier, LavalinkTrackInfo info) : this(relatedSpotifyTrack, identifier, info.Author,
            info.Duration, info.IsLiveStream, info.IsSeekable, info.Source, info.Title, info.TrackIdentifier, StreamProviderUtil.GetStreamProvider(info.Source!)) { }

        public SpotifyLavalinkTrack(SpotifyTrack relatedSpotifyTrack, string identifier, string author, TimeSpan duration, bool isLiveStream, bool isSeekable,
                                    string? source, string title, string trackIdentifier, StreamProvider provider) : base(identifier, author, duration,
            isLiveStream, isSeekable, source, title, trackIdentifier, provider) {
            RelatedSpotifyTrack = relatedSpotifyTrack;
        }
    }
}