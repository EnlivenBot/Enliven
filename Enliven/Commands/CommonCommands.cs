using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common.Localization.Entries;
using Discord.Commands;

namespace Bot.Commands;

[SlashCommandAdapter]
public class CommonCommands : AdvancedModuleBase {
    [Command("vote")]
    [Alias("support", "voting")]
    public async Task Vote() {
        await this.ReplyFormattedAsync(new EntryLocalized("Common.Vote"), new EntryLocalized("Common.VoteDescription"));
        await this.RemoveMessageInvokerIfPossible();
    }
}