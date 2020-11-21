using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Common.Config;
using Discord.Commands;

namespace Bot.Commands {
    [Hidden]
    public class Properties : AdvancedModuleBase {
        [Command("enablelogging")]
        public async Task EnableLogging(bool b) {
            GuildConfig.IsLoggingEnabled = b;
            GuildConfig.Save();
            await ReplyAsync(Loc.Get(b ? "Commands.LoggingEnabled" : "Commands.LoggingDisabled"));
        }
        
        [Command("enablecommandslogging")]
        public async Task EnableCommandsLogging(bool b) {
            GuildConfig.IsCommandLoggingEnabled = b;
            GuildConfig.Save();
            await ReplyAsync(Loc.Get(b ? "Commands.CommandLoggingEnabled" : "Commands.CommandLoggingDisabled"));
        }

        [Command("limitmusiccommands")]
        public async Task LimitMusicCommand(bool b) {
            if (b && !GuildConfig.GetChannel(ChannelFunction.Music, out _)) {
                await ReplyAsync("You must set music channel first");
                return;
            }

            GuildConfig.IsMusicLimited = b;
            GuildConfig.Save();
            await ReplyAsync(GuildConfig.IsMusicLimited ? "Music now limited in music channel" : "Music now now allowed in any channel");
        }
    }
}