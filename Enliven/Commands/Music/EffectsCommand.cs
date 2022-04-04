using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Music;
using Common;
using Common.Music.Effects;
using Discord.Commands;

namespace Bot.Commands.Music {
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public class EffectsCommand : MusicModuleBase {
        public EmbedPlayerEffectsDisplayProvider EmbedPlayerEffectsDisplayProvider { get; set; } = null!;
        [Command("effects", RunMode = RunMode.Async)]
        [Alias("efs")]
        [Summary("effects0s")]
        public async Task Effects() {
            if (Player == null || Player.Playlist.IsEmpty) {
                await ReplyFormattedAsync(Loc.Get("Music.QueueEmpty").Format(GuildConfig.Prefix), true);
                return;
            }

            EmbedPlayerEffectsDisplayProvider.CreateOrUpdateQueueDisplay(Context.Channel, Player);
        }
    }
}