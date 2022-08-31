using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Common;
using Common.Localization.Entries;
using Discord.Commands;

namespace Bot.Commands {
    [Grouping("utils")]
    public class UtilsCommand : AdvancedModuleBase {
        [Command("invite", RunMode = RunMode.Async)]
        [Alias("link")]
        [Summary("invite0s")]
        public async Task Invite() {
            await this.ReplyFormattedAsync(new EntryLocalized("Common.Invite"), new EntryLocalized("Common.InviteDescription"));
            await this.RemoveMessageInvokerIfPossible();
        }
    }
}