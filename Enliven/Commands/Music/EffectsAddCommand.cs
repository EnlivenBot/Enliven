using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common;
using Common.Config;
using Common.Localization.Entries;
using Discord.Commands;
using LiteDB;
using Tyrrrz.Extensions;

namespace Bot.Commands.Music;

[SlashCommandAdapter]
[Grouping("music")]
[RequireContext(ContextType.Guild)]
public class EffectsAddCommand : HavePlayerMusicModuleBase {
    public IUserDataProvider UserDataProvider { get; set; } = null!;
    public ILiteCollection<PlayerEffect> PlayerEffectCollection { get; set; } = null!;

    [Command("effects add", RunMode = RunMode.Async)]
    [Priority(100)]
    [Alias("ef add", "effect add")]
    [Summary("effects_add0s")]
    public async Task AddEffect([Summary("effect_add0_0s")] [Remainder] string effectJson = "") {
        if (effectJson.IsNullOrWhiteSpace()) {
            await this.ReplyFormattedAsync(new EntryLocalized("Effects.AddEffectTitle"), new EntryLocalized("Effects.AddEffectDescription"));
            return;
        }

        try {
            var effectBase = PlayerEffectBase.FromDefaultJson(effectJson!);
            if (effectBase == null) throw new Exception("Looks like JSON is empty");
            if (!effectBase.IsValid(out var errorEntry)) {
                await this.ReplyFailFormattedAsync(errorEntry, true);
                return;
            }

            var playerEffect = new PlayerEffect(effectBase, Context.User.ToLink());
            PlayerEffectCollection.Insert(playerEffect);
            var userData = Context.User.ToLink().GetData(UserDataProvider);
            userData.PlayerEffects.Add(playerEffect);
            userData.Save();
        }
        catch (Exception e) {
            await this.ReplyFailFormattedAsync(e.Message.ToEntry(), true);
        }
    }
}