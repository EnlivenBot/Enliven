using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common;
using Common.Config;
using Discord.Commands;
using LiteDB;
using Newtonsoft.Json;
using Tyrrrz.Extensions;

namespace Bot.Commands.Music {
    [SlashCommandAdapter]
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public class AddEffectCommand : MusicModuleBase {
        public IUserDataProvider UserDataProvider { get; set; } = null!;
        public ILiteCollection<PlayerEffect> PlayerEffectCollection { get; set; } = null!;
        
        // TODO Implement this command as subcommand for effect
        [SlashCommandAdapter(false)]
        [Command("effect add", RunMode = RunMode.Async)]
        [Priority(100)]
        [Alias("ef add")]
        [Summary("effect_add0s")]
        public async Task AddEffect([Summary("effect_add0_0s")][Remainder]string effectJson = "") {
            if (effectJson.IsNullOrWhiteSpace()) {
                await ReplyFormattedAsync(Loc.Get("Effects.AddEffectTitle"), Loc.Get("Effects.AddEffectDescription", GuildConfig.Prefix));
                return;
            }

            try {
                var effectBase = PlayerEffectBase.FromDefaultJson(effectJson!);
                if (effectBase == null) throw new Exception("Looks like JSON is empty");
                if (!effectBase.IsValid(out var errorEntry)) {
                    await ReplyFormattedAsync(errorEntry.Get(Loc), true);
                    return;
                }

                var playerEffect = new PlayerEffect(effectBase, Context.User.ToLink());
                PlayerEffectCollection.Insert(playerEffect);
                var userData = Context.User.ToLink().GetData(UserDataProvider);
                userData.PlayerEffects.Add(playerEffect);
                userData.Save();
            }
            catch (Exception e) {
                await ReplyFormattedAsync(e.Message, true);
            }
        }
    }
}