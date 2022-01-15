using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Common;
using Discord.Commands;

namespace Bot.Commands {
    [Grouping("utils")]
    public class UtilsCommand : AdvancedModuleBase {
        [Command("invite", RunMode = RunMode.Async)]
        [Alias("link")]
        [Summary("invite0s")]
        public async Task Invite() {
            Context.Message.SafeDelete();
            await ReplyFormattedAsync(Loc.Get("Common.Invite"), Loc.Get("Common.InviteDescription"));
        }
    }
}