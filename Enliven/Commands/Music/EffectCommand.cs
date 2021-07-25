using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Common;
using Common.Music.Effects;
using Discord.Commands;

namespace Bot.Commands.Music {
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public class EffectCommand : MusicModuleBase {
        public EffectSourceProvider EffectSourceProvider { get; set; } = null!;

        [Command("effect", RunMode = RunMode.Async)]
        [Alias("ef")]
        [Summary("effect0s")]
        public async Task Effect([Summary("effect0_0s")]string effectName, [Summary("effect0_1s")][Remainder]string? args = "") {
            if (!await IsPreconditionsValid) return;

            if (await TryRemoveEffect(effectName)) return;

            if (!EffectSourceProvider.TryResolve(Context.User.ToLink(), effectName, out var effectSource)) {
                await ReplyFormattedAsync(Loc.Get("Music.EffectNotFound"), true);
                return;
            }
            
            if (await TryRemoveEffect(effectSource.GetSourceName())) return;

            if (Player!.Effects.Count >= 5) {
                await ReplyFormattedAsync(Loc.Get("Music.MaxEffectsCountExceed"), true);
                return;
            }
            var playerEffect = await effectSource.CreateEffect(args);
            await Player!.ApplyEffect(playerEffect, Context.User);
        }

        private async Task<bool> TryRemoveEffect(string effectName) {
            var currentEffectUse = Player!.Effects.FirstOrDefault(use => use.Effect.SourceName == effectName || use.Effect.DisplayName == effectName);
            if (currentEffectUse == null) return false;
            await Player.RemoveEffect(currentEffectUse, Context.User);
            return true;
        }
    }
}