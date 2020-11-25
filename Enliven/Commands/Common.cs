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
        [Command("invite", RunMode = RunMode.Async)]
        [Alias("link")]
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