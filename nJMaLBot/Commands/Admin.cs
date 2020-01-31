using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities.Commands;
using Discord.Commands;

namespace Bot.Commands {
    [Hidden]
    public class AdminCommands : AdvancedModuleBase {
        [Command("limitmusiccommands.")]
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