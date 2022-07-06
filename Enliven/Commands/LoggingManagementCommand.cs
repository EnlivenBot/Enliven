using System;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageHistories;
using Bot.Utilities.Collector;
using Common;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    [SlashCommandAdapter]
    [Grouping("admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RegisterIf(typeof(RegisterIf.LoggingEnabled))]
    public class LoggingManagementCommand : AdvancedModuleBase {
        public MessageHistoryService MessageHistoryService { get; set; } = null!;
        public CollectorService CollectorService { get; set; } = null!;
        public EnlivenShardedClient EnlivenShardedClient { get; set; } = null!;
        
        [Command("clearhistories", RunMode = RunMode.Async)]
        [Summary("clearhistories0s")]
        public async Task ClearHistories() {
            await ReplyAsync("Start clearing message histories");
            await MessageHistoryService.ClearGuildLogs((SocketGuild) Context.Guild);
        }

        [Command("logging")]
        [Summary("logging0s")]
        public async Task LoggingControlPanel() {
            var botPermissions = (await Context.Guild.GetUserAsync(Context.Client.CurrentUser.Id)).GetPermissions((IGuildChannel) Context.Channel);
            if (botPermissions.SendMessages) {
                await new LoggingChain((ITextChannel) Context.Channel, Context.User, GuildConfig, CollectorService, EnlivenShardedClient).Start();
            }
            else {
                await (await Context.User.CreateDMChannelAsync()).SendMessageAsync(string.Format($"<#{Context.Channel.Id}>"));
            }
        }
    }
}