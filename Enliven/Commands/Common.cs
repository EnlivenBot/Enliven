using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Logging;
using Common;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    [Grouping("utils")]
    public class UtilsCommand : AdvancedModuleBase {
        [Alias("log")]
        [Command("history", RunMode = RunMode.Async)]
        [Summary("history0s")]
        public async Task PrintChangesCommand([Remainder] [Summary("history0_0s")] string id) {
            await PrintChanges(id, false);
        }

        [Command("renderlog", RunMode = RunMode.Async)]
        [Summary("renderlog0s")]
        public async Task PrintImageChangesCommand([Remainder] [Summary("history0_0s")] string id) {
            await PrintChanges(id, true);
        }

        private async Task PrintChanges(string id, bool forceImage) {
            id = id.Trim();
            var channelId = Context.Channel.Id;
            ulong messageId = 0;
            try {
                if (id.Contains('-')) {
                    channelId = Convert.ToUInt64(id.Split('-')[0]);
                    messageId = Convert.ToUInt64(id.Split('-')[1]);
                }
                else {
                    messageId = Convert.ToUInt64(id);
                }
            }
            catch (Exception) {
                await ReplyFormattedAsync(Loc.Get("CommandHandler.FailedTitle"),
                    Loc.Get("MessageHistory.IdFailedToParse").Format(id.SafeSubstring(100, "..."), GuildConfig.Prefix), 
                    Constants.StandardTimeSpan);
                Context.Message.SafeDelete();
                return;
            }

            await MessageHistoryManager.PrintLog(MessageHistory.Get(channelId, Convert.ToUInt64(messageId)),
                (SocketTextChannel) await GetResponseChannel(), Loc, (IGuildUser) Context.User, forceImage);
            Context.Message.SafeDelete();
        }

        [Command("stats", RunMode = RunMode.Async)]
        [Summary("stats0s")]
        public Task Stats() {
            Context.Message.SafeDelete();
            ReplyAsync(null, false, StatsUtils.BuildStats(null, Loc).Build()).DelayedDelete(Constants.StandardTimeSpan);
            return Task.CompletedTask;
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats0s")]
        public Task UserStats([Summary("userstats0_0s")] IUser user) {
            Context.Message.SafeDelete();
            ReplyAsync(null, false, StatsUtils.BuildStats(user, Loc).Build()).DelayedDelete(Constants.StandardTimeSpan);
            return Task.CompletedTask;
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats1s")]
        public async Task UserStats() {
            await UserStats(Context.User);
        }

        [Command("invite", RunMode = RunMode.Async)]
        [Summary("invite0s")]
        public async Task Invite([Summary("invite0_0s")] bool emptyPermissions = false) {
            Context.Message.SafeDelete();
            var inviteUrl = $"https://discordapp.com/api/oauth2/authorize?client_id={Program.Client.CurrentUser.Id}&permissions=1110764608&scope=bot";
            await ReplyFormattedAsync(Loc.Get("Common.Invite"), Loc.Get("Common.InviteDescription").Format(inviteUrl));
        }
    }

    public class CommonCommands : AdvancedModuleBase {
        [Command("vote")]
        [Alias("support", "voting")]
        public async Task Vote() {
            Context.Message.SafeDelete();
            await ReplyFormattedAsync(Loc.Get("Common.Vote"), Loc.Get("Common.VoteDescription"));
        }
    }
}