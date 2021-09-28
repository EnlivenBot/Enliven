using System;
using System.Threading.Tasks;
using Common;
using Common.Localization.Providers;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

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
        
        public static EmbedBuilder WithRequester(this EmbedBuilder builder, IUser user, ILocalizationProvider loc) {
            builder.WithFooter(loc.Get("Commands.RequestedBy").Format(user.Username), user.GetAvatarUrl());
            return builder;
        }
        
        public static EnlivenEmbedBuilder WithRequester(this EnlivenEmbedBuilder builder, IUser user, ILocalizationProvider loc) {
            builder.WithFooter(loc.Get("Commands.RequestedBy").Format(user.Username), user.GetAvatarUrl());
            return builder;
        }

        public static PriorityEmbedFieldBuilder ToWrapper(this EmbedFieldBuilder builder, int? priority = null, bool enabled = true) {
            return new PriorityEmbedFieldBuilder()
                .WithName(builder.Name)
                .WithValue(builder.Value)
                .WithIsInline(builder.IsInline)
                .WithPriority(priority)
                .WithEnabled(enabled);
        }

        public static async Task<IGuildUser> GetUser(this IGuild guild, ulong id) {
            return guild switch {
                RestGuild restGuild     => await restGuild.GetUserAsync(id),
                SocketGuild socketGuild => socketGuild.GetUser(id),
                _                       => throw new ArgumentOutOfRangeException(nameof(guild))
            };
        }
    }
}