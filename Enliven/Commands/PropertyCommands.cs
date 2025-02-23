using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common.Config;
using Common.Localization.Entries;
using Discord.Commands;

namespace Bot.Commands;

[Hidden]
[SlashCommandAdapter]
public class PropertyCommands : AdvancedModuleBase {
    [Command("limitmusiccommands")]
    public async Task LimitMusicCommand(bool shouldLimitMusicCommands) {
        if (shouldLimitMusicCommands && !GuildConfig.GetChannel(ChannelFunction.Music, out _)) {
            // TODO: Replace with EntryLocalized
            await this.ReplySuccessFormattedAsync(new EntryString("You must set music channel first"));
            return;
        }

        GuildConfig.IsMusicLimited = shouldLimitMusicCommands;
        GuildConfig.Save();
        var description = new EntryLocalized(GuildConfig.IsMusicLimited ? "Music now limited in music channel" : "Music now now allowed in any channel");
        await this.ReplySuccessFormattedAsync(description);
    }
}