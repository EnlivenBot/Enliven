using System;
using Lavalink4NET.Player;

namespace Bot.DiscordRelated.Music.Tracks {
    public class AuthoredTrack : LavalinkTrack {
        private string? Requester { get; set; }
        public LavalinkTrack Track { get; set; }

        public AuthoredTrack(LavalinkTrack track, string? requester) : base(track.Identifier, track.Author, track.Duration, track.IsLiveStream,
            track.IsSeekable, track.Source, track.Title, track.TrackIdentifier, track.Provider) {
            Requester = requester;
            Track = track;
        }

        public virtual string GetRequester() {
            return Requester ?? "Unknown";
        }

        public override string Author => Track.Author;

        public override TimeSpan Duration => Track.Duration;

        public override string Identifier => Track.Identifier;

        public override bool IsLiveStream => Track.IsLiveStream;

        public override bool IsSeekable => Track.IsSeekable;

        public override TimeSpan Position => Track.Position;

        public override StreamProvider Provider => Track.Provider;

        public override string? Source => Track.Source;

        public override string Title => Track.Title;

        public override string TrackIdentifier => Track.TrackIdentifier;
    }
}