using System;

namespace Bot.DiscordRelated.Commands.Modules {
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class RequireNonEmptyPlaylistAttribute : Attribute {
        public RequireNonEmptyPlaylistAttribute(bool requirePlayingTrack = false) {
            RequirePlayingTrack = requirePlayingTrack;
        }
        public bool RequirePlayingTrack { get; }
    }
}