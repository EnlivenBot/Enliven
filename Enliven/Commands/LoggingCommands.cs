using System;
using System.Threading.Tasks;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageHistories;
using Common;
using Common.Localization.Entries;
using Discord;
using Discord.Commands;

namespace Bot.Commands {
    [SlashCommandAdapter]
    [RegisterIf(typeof(RegisterIf.LoggingEnabled))]
    public class LoggingCommands : AdvancedModuleBase {
        public MessageHistoryService MessageHistoryService { get; set; } = null!;

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
                await this.ReplyFailFormattedAsync(new EntryLocalized("MessageHistory.IdFailedToParse", id.SafeSubstring(100, "..."))).CleanupAfter(Constants.StandardTimeSpan);
                await this.RemoveMessageInvokerIfPossible();
                return;
            }

            IUserMessage? userMessage = null;
            if (forceImage) {
                try {
                    userMessage = await Context.Client.GetChannelAsync(channelId).PipeAsync(channel => (channel as ITextChannel)?.GetMessageAsync(messageId)!) as IUserMessage;
                }
                catch (Exception) {
                    // ignored
                }
            }

            var bot = (await Context.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel) Context.Channel);
            var channel = bot.SendMessages ? Context.Channel : await Context.User.CreateDMChannelAsync();
            // TODO: Make MessageHistoryService support interactions
            await MessageHistoryService.PrintLog(channelId, messageId, userMessage, channel, Loc, (IGuildUser) Context.User, forceImage);
            await this.RemoveMessageInvokerIfPossible();
        }
    }
}