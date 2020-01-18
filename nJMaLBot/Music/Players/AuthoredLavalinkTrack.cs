using System;
using Discord;
using Lavalink4NET.Player;
using Lavalink4NET.Util;

namespace Bot.Music.Players {
    public class AuthoredLavalinkTrack : LavalinkTrack {
        public LavalinkTrack Track { get; set; }
        public string RequesterName { get; set; }
        public IUser Requester { get; set; }


        public string GetRequester() {
            return Requester?.Username ?? RequesterName ?? "Unknown";
        }

        public AuthoredLavalinkTrack(string identifier, LavalinkTrackInfo info) : base(identifier, info) { }

        public AuthoredLavalinkTrack(string identifier, string author, TimeSpan duration, bool isLiveStream, bool isSeekable, string source, string title,
                                     string trackIdentifier, StreamProvider provider) : base(identifier, author, duration, isLiveStream, isSeekable, source,
            title, trackIdentifier, provider) { }

        public static AuthoredLavalinkTrack FromLavalinkTrack(LavalinkTrack track) {
            return new AuthoredLavalinkTrack(track.Identifier, track.Author, track.Duration, track.IsLiveStream, track.IsSeekable, track.Source, track.Title,
                track.TrackIdentifier, StreamProviderUtil.GetStreamProvider(track.Source));
        }

        public static AuthoredLavalinkTrack FromLavalinkTrack(LavalinkTrack track, IUser requester) {
            var newTrack = FromLavalinkTrack(track);
            newTrack.Requester = requester;
            return newTrack;
        }

        public static AuthoredLavalinkTrack FromLavalinkTrack(LavalinkTrack track, string requesterName) {
            var newTrack = FromLavalinkTrack(track);
            newTrack.RequesterName = requesterName;
            return newTrack;
        }
    }
}