using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.MessageHistories;
using Common;
using Common.Entities;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    public class Log : AdvancedModuleBase {
        public MessageHistoryService MessageHistoryService { get; set; } = null!;
        public MessageHistoryProvider MessageHistoryProvider { get; set; } = null!;
        
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
            ulong messageId;
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

            await MessageHistoryService.PrintLog(channelId, messageId, await GetResponseChannel(), Loc, (IGuildUser) Context.User, forceImage);
            Context.Message.SafeDelete();
        }
    }
}