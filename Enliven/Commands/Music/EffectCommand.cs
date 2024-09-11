using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common;
using Common.Localization.Entries;
using Common.Music.Effects;
using Common.Music.Players;
using Discord.Commands;

namespace Bot.Commands.Music;

[SlashCommandAdapter]
[Grouping("music")]
[RequireContext(ContextType.Guild)]
public class EffectCommand : HavePlayerMusicModuleBase
{
    public EffectSourceProvider EffectSourceProvider { get; set; } = null!;

    [RequireNonEmptyPlaylist]
    [Command("effect", RunMode = RunMode.Async)]
    [Alias("ef")]
    [Summary("effect0s")]
    public async Task Effect([Summary("effect0_0s")] string effectName,
        [Summary("effect0_1s")] [Remainder] string? args = "")
    {
        if (await TryRemoveEffect(effectName)) return;

        if (!EffectSourceProvider.TryResolve(Context.User.ToLink(), effectName, out var effectSource))
        {
            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.EffectNotFound"), true);
            return;
        }

        if (await TryRemoveEffect(effectSource.GetSourceName())) return;

        if (Player.Effects.Count >= PlayerConstants.MaxEffectsCount)
        {
            await this.ReplyFailFormattedAsync(
                new EntryLocalized("Music.MaxEffectsCountExceed", PlayerConstants.MaxEffectsCount), true);
            return;
        }

        var playerEffect = await effectSource.CreateEffect(args);
        await Player.ApplyEffect(playerEffect, Context.User);
    }

    private async Task<bool> TryRemoveEffect(string effectName)
    {
        var currentEffectUse = Player.Effects.FirstOrDefault(use =>
            use.Effect.SourceName == effectName || use.Effect.DisplayName == effectName);
        if (currentEffectUse == null) return false;
        await Player.RemoveEffect(currentEffectUse, Context.User);
        return true;
    }
}