using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Bot.Utilities.Modules;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Tyrrrz.Extensions;

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

            await MessageHistoryManager.PrintLog(Convert.ToUInt64(messageId), channelId, (SocketTextChannel) Context.Channel, (IGuildUser) Context.User);
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

        [Command("invite", RunMode = RunMode.Async)]
        [Summary("invite0s")]
        public async Task Invite([Summary("invite0_0s")]bool emptyPermissions = false) {
            Context.Message.SafeDelete();
            var inviteUrl = $"https://discordapp.com/api/oauth2/authorize?client_id={Program.Client.CurrentUser.Id}&permissions={(emptyPermissions ? 0 : 8)}&scope=bot";
            await ReplyFormattedAsync(Loc.Get("Common.Invite"), Loc.Get("Common.InviteDescription").Format(inviteUrl));
        }
    }
}