using System;
using System.Threading.Tasks;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Bot.Utilities.Modules;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    [Grouping("utils")]
    public class LogCommand : AdvancedModuleBase {
        [Command("history", RunMode = RunMode.Async)]
        [Summary("history0s")]
        public async Task PrintChanges(
            [Remainder] [Summary("history0_0s")] string id) {
            id = id.Trim();
            var channelId = Context.Channel.Id;
            var messageId = id;
            if (id.Contains('-')) {
                channelId = Convert.ToUInt64(id.Split('-')[0]);
                messageId = id.Split('-')[1];
            }

            await MessageHistoryManager.PrintLog(MessageHistory.Get(channelId, Convert.ToUInt64(messageId)),
                (SocketTextChannel) await GetResponseChannel(), Loc, (IGuildUser) Context.User);
            Context.Message.SafeDelete();
        }

        [Command("stats", RunMode = RunMode.Async)]
        [Summary("stats0s")]
        public async Task Stats() {
            Context.Message.SafeDelete();
            ReplyAsync(null, false, (await StatsUtils.PrintStats(null, Loc)).Build()).DelayedDelete(TimeSpan.FromMinutes(5));;
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats0s")]
        public async Task UserStats([Summary("userstats0_0s")] IUser user) {
            Context.Message.SafeDelete();
            ReplyAsync(null, false, (await StatsUtils.PrintStats(user, Loc)).Build()).DelayedDelete(TimeSpan.FromMinutes(5));;
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats1s")]
        public async Task UserStats() {
            Context.Message.SafeDelete();
            ReplyAsync(null, false, (await StatsUtils.PrintStats(Context.User, Loc)).Build()).DelayedDelete(TimeSpan.FromMinutes(5));
        }
    }
}