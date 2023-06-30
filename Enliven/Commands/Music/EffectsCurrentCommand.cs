using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.Music;
using Discord.Commands;

namespace Bot.Commands.Music;

[SlashCommandAdapter]
[Grouping("music")]
[RequireContext(ContextType.Guild)]
public class EffectsCurrentCommand : MusicModuleBase {
    public EmbedPlayerEffectsDisplayProvider EmbedPlayerEffectsDisplayProvider { get; set; } = null!;

    [RequireNonEmptyPlaylist]
    [Command("effects current", RunMode = RunMode.Async)]
    [Alias("efs current", "effects", "efs")]
    [Summary("effects_current0ss")]
    public Task EffectsCurrent() {
        return EmbedPlayerEffectsDisplayProvider.CreateOrUpdateQueueDisplay(Context.Channel, Player);
    }
}