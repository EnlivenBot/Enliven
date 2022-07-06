using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
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
        public Task Stats() {
            Context.Message.SafeDelete();
            _ = ReplyAsync(null, false, StatisticsService.BuildStats(null, Loc).Build()).DelayedDelete(Constants.StandardTimeSpan);
            return Task.CompletedTask;
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats0s")]
        public Task UserStats([Summary("userstats0_0s")] IUser? user = null) {
            Context.Message.SafeDelete();
            var u = user ?? Context.User;
            _ = ReplyAsync(null, false, StatisticsService.BuildStats(u, Loc).Build()).DelayedDelete(Constants.StandardTimeSpan);
            return Task.CompletedTask;
        }
    }
}