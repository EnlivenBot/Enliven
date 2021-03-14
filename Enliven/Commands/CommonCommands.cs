using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules;
using Common;
using Discord.Commands;

namespace Bot.Commands {
    public class CommonCommands : AdvancedModuleBase {
        [Command("vote")]
        [Alias("support", "voting")]
        public async Task Vote() {
            Context.Message.SafeDelete();
            await ReplyFormattedAsync(Loc.Get("Common.Vote"), Loc.Get("Common.VoteDescription"));
        }
    }
}