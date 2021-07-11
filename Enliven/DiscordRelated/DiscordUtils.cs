using Common;
using Common.Localization.Providers;
using Discord;

namespace Bot.DiscordRelated {
    public static class DiscordUtils {
        public static EmbedBuilder GetAuthorEmbedBuilder(IUser user, ILocalizationProvider loc) {
            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithFooter(loc.Get("Commands.RequestedBy").Format(user.Username), user.GetAvatarUrl());
            return embedBuilder;
        }
        
        public static EnlivenEmbedBuilder GetAuthorEmbedBuilderWrapper(IUser user, ILocalizationProvider loc) {
            var embedBuilder = new EnlivenEmbedBuilder();
            embedBuilder.WithFooter(loc.Get("Commands.RequestedBy").Format(user.Username), user.GetAvatarUrl());
            return embedBuilder;
        }
    }
}