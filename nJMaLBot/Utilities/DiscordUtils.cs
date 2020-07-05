using Bot.Config.Localization.Providers;
using Discord;

namespace Bot.Utilities {
    public static class DiscordUtils {
        public static EmbedBuilder GetAuthorEmbedBuilder(IUser user, ILocalizationProvider loc) {
            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithFooter(loc.Get("Commands.RequestedBy").Format(user.Username), user.GetAvatarUrl());
            return embedBuilder;
        }
        
        public static PriorityEmbedBuilderWrapper GetAuthorEmbedBuilderWrapper(IUser user, ILocalizationProvider loc) {
            var embedBuilder = new PriorityEmbedBuilderWrapper();
            embedBuilder.WithFooter(loc.Get("Commands.RequestedBy").Format(user.Username), user.GetAvatarUrl());
            return embedBuilder;
        }
    }
}