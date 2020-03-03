using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities.Commands;
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
    }
}