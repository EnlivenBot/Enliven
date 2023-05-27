using System;
using Discord;

namespace Common.Music.Tracks {
    public interface ITrackHasCustomSource {
        public Emote CustomSourceEmote { get; }
        public Uri CustomSourceUrl { get; }
    }
}