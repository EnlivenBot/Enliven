using Common.Localization.Providers;
using Discord;

namespace Bot.DiscordRelated.Commands {
    public interface IStatisticsService {
        EmbedBuilder BuildStats(IUser? user, ILocalizationProvider loc);
    }
}