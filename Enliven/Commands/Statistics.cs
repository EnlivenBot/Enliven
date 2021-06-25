using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Common;
using Discord;
using Discord.Commands;

namespace Bot.Commands {
    public class Statistics : AdvancedModuleBase {
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
        public Task UserStats([Summary("userstats0_0s")] IUser user) {
            Context.Message.SafeDelete();
            _ = ReplyAsync(null, false, StatisticsService.BuildStats(user, Loc).Build()).DelayedDelete(Constants.StandardTimeSpan);
            return Task.CompletedTask;
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats1s")]
        public async Task UserStats() {
            await UserStats(Context.User);
        }
    }
}