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
    [Command("enablelogging")]
    public async Task EnableLogging(bool shouldEnableLogging) {
        GuildConfig.IsLoggingEnabled = shouldEnableLogging;
        GuildConfig.Save();
        var description = new EntryLocalized(shouldEnableLogging ? "Commands.LoggingEnabled" : "Commands.LoggingDisabled");
        await this.ReplySuccessFormattedAsync(description);
    }

    [Command("enablecommandslogging")]
    public async Task EnableCommandsLogging(bool shouldEnableCommandsLogging) {
        GuildConfig.IsCommandLoggingEnabled = shouldEnableCommandsLogging;
        GuildConfig.Save();
        var description = new EntryLocalized(shouldEnableCommandsLogging ? "Commands.CommandLoggingEnabled" : "Commands.CommandLoggingDisabled");
        await this.ReplySuccessFormattedAsync(description);
    }

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