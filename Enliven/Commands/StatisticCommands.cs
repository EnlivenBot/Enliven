using System.Threading.Tasks;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Commands.Modules.Contexts;
using Bot.DiscordRelated.Interactions;
using Common;
using Discord;
using Discord.Commands;

namespace Bot.Commands {
    [SlashCommandAdapter]
    public class StatisticCommands : AdvancedModuleBase {
        public IStatisticsService StatisticsService { get; set; } = null!;

        [Command("stats", RunMode = RunMode.Async)]
        [Summary("stats0s")]
        public async Task Stats() {
            await Context.SendMessageAsync(null, StatisticsService.BuildStats(null, Loc).Build()).CleanupAfter(Constants.StandardTimeSpan);
            await this.RemoveMessageInvokerIfPossible();
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats0s")]
        public async Task UserStats([Summary("userstats0_0s")] IUser? user = null) {
            var u = user ?? Context.User;
            await Context.SendMessageAsync(null, StatisticsService.BuildStats(u, Loc).Build()).CleanupAfter(Constants.StandardTimeSpan);
            await this.RemoveMessageInvokerIfPossible();
        }
    }
}