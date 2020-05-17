using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Commands;
using Bot.Utilities.Modules;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    [Grouping("admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class AdminCommands : AdvancedModuleBase {
        [Command("setprefix")]
        [Summary("setprefix0s")]
        public async Task SetPrefix([Summary("setrefix0_0s")] string prefix) {
            GuildConfig.Get(Context.Guild.Id).SetServerPrefix(prefix).Save();
            await ReplyFormattedAsync(Loc.Get("Commands.Success"), Loc.Get("Commands.SetPrefixResponse").Format(prefix), TimeSpan.FromMinutes(10));
            Context.Message.SafeDelete();
        }

        [Command("language")]
        [Alias("languages")]
        [Summary("language0s")]
        public async Task ListLanguages() {
            var embedBuilder = this.GetAuthorEmbedBuilder().WithColor(Color.Gold).WithTitle(Loc.Get("Localization.LanguagesList"));
            foreach (var (key, pack) in Localization.Languages) {
                embedBuilder.AddField($"{pack.LocalizationFlagEmojiText} **{pack.LocalizedName}** ({pack.LanguageName})",
                    Loc.Get("Localization.LanguageDescription").Format(GuildConfig.Prefix, key, pack.Authors, pack.TranslationCompleteness), true);
            }

            var message = await ReplyAsync(null, false, embedBuilder.Build());
            CollectorsGroup collectors = null;
            var packsWithEmoji = Localization.Languages.Where(pair => pair.Value.LocalizationFlagEmoji != null).ToList();
            collectors = new CollectorsGroup(packsWithEmoji.Select(
                pair => {
                    var packName = pair.Key;
                    return CollectorsUtils.CollectReaction(message, reaction => reaction.Emote.Equals(pair.Value.LocalizationFlagEmoji), async args => {
                        await args.RemoveReason();
                        var t = await Program.Handler.ExecuteCommand($"setlanguage {packName}", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                        if (t.IsSuccess) {
                            message.SafeDelete();
                            collectors?.DisposeAll();
                        }
                    }, CollectorFilter.IgnoreBots);
                }));
            try {
                await message.AddReactionsAsync(packsWithEmoji.Select(pair => pair.Value.LocalizationFlagEmoji).ToArray());
            }
            catch (Exception e) {
                // ignored
            }
            message.DelayedDelete(TimeSpan.FromMinutes(5));
        }

        [Command("setlanguage")]
        [Summary("setlanguage0s")]
        public async Task SetLanguage([Summary("setlanguage0_0s")] string language = null) {
            if (language == null) {
                await ListLanguages();
                return;
            }

            if (Localization.Languages.ContainsKey(language)) {
                GuildConfig.Get(Context.Guild.Id).SetLanguage(language).Save();
                Context.Message.SafeDelete();
                await ReplyFormattedAsync(Loc.Get("Commands.Success"), Loc.Get("Localization.Success").Format(language), TimeSpan.FromMinutes(1));
            }
            else {
                var languagesList = string.Join(' ', Localization.Languages.Select(pair => $"`{pair.Key}`"));
                await ReplyFormattedAsync(Loc.Get("Commands.Fail"), Loc.Get("Localization.Fail").Format(language, languagesList), TimeSpan.FromMinutes(1));
            }
        }

        [Command("setchannel")]
        [Summary("setchannel0s")]
        public async Task SetChannel([Summary("setchannel0_0s")] ChannelFunction func,
                                     [Summary("setchannel0_1s")] IChannel channel) {
            GuildConfig.Get(Context.Guild.Id).SetChannel(channel.Id.ToString(), func).Save();
            await ReplyFormattedAsync(Loc.Get("Commands.Success"), Loc.Get("Commands.SetChannelResponse").Format(channel.Id, func.ToString()));
            Context.Message.SafeDelete();
        }

        [Command("setchannel")]
        [Summary("setchannel0s")]
        public async Task SetThisChannel([Summary("setchannel0_1s")] ChannelFunction func) {
            await SetChannel(func, Context.Channel);
        }

        [Command("clearhistories", RunMode = RunMode.Async)]
        [Summary("clearhistories0s")]
        public async Task ClearHistories() {
            await ReplyAsync("Start clearing message histories");
            await MessageHistoryManager.ClearGuildLogs((SocketGuild) Context.Guild);
        }
    }
}