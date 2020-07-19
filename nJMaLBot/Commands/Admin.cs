using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.Config;
using Bot.Config.Localization;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Logging;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    [Grouping("admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class AdminCommands : AdvancedModuleBase {
        [Hidden]
        [Command("printwelcome")]
        public async Task PrintWelcome() {
            Context.Message.SafeDelete();
            (await GlobalBehaviors.PrintWelcomeMessage((SocketGuild) Context.Guild, Context.Channel)).DelayedDelete(TimeSpan.FromMinutes(10));
        }
        
        [Command("setprefix")]
        [Summary("setprefix0s")]
        public async Task SetPrefix([Summary("setrefix0_0s")] string prefix) {
            var guildConfig = GuildConfig.Get(Context.Guild.Id);
            guildConfig.Prefix = prefix;
            guildConfig.Save();
            await ReplyFormattedAsync(Loc.Get("Commands.Success"), Loc.Get("Commands.SetPrefixResponse").Format(prefix), TimeSpan.FromMinutes(10));
            Context.Message.SafeDelete();
        }

        [Command("language")]
        [Alias("languages")]
        [Summary("language0s")]
        public async Task ListLanguages() {
            var embedBuilder = this.GetAuthorEmbedBuilder().WithColor(Color.Gold).WithTitle(Loc.Get("Localization.LanguagesList"));
            foreach (var (key, pack) in LocalizationManager.Languages) {
                embedBuilder.AddField($"{pack.LocalizationFlagEmojiText} **{pack.LocalizedName}** ({pack.LanguageName})",
                    Loc.Get("Localization.LanguageDescription").Format(GuildConfig.Prefix, key, pack.Authors, pack.TranslationCompleteness), true);
            }

            var message = await ReplyAsync(null, false, embedBuilder.Build());
            CollectorsGroup collectors = null!;
            var packsWithEmoji = LocalizationManager.Languages.Where(pair => pair.Value.LocalizationFlagEmoji != null).ToList();
            collectors = new CollectorsGroup(packsWithEmoji.Select(
                pair => {
                    var packName = pair.Key;
                    return CollectorsUtils.CollectReaction(message, reaction => reaction.Emote.Equals(pair.Value.LocalizationFlagEmoji), async args => {
                        await args.RemoveReason();
                        var t = await Program.Handler.ExecuteCommand($"setlanguage {packName}", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                        if (t.IsSuccess) {
                            message.SafeDelete();
                            // ReSharper disable once AccessToModifiedClosure
                            collectors?.DisposeAll();
                        }
                    }, CollectorFilter.IgnoreBots);
                }));
            try {
                await message.AddReactionsAsync(packsWithEmoji.Select(pair => pair.Value.LocalizationFlagEmoji).ToArray());
            }
            catch (Exception) {
                // ignored
            }
            message.DelayedDelete(TimeSpan.FromMinutes(5));
        }

        [Command("setlanguage")]
        [Summary("setlanguage0s")]
        public async Task SetLanguage([Summary("setlanguage0_0s")] string? language = null) {
            if (language == null) {
                await ListLanguages();
                return;
            }

            if (LocalizationManager.Languages.ContainsKey(language)) {
                GuildConfig.Get(Context.Guild.Id).SetLanguage(language).Save();
                Context.Message.SafeDelete();
                await ReplyFormattedAsync(Loc.Get("Commands.Success"), Loc.Get("Localization.Success").Format(language), TimeSpan.FromMinutes(1));
            }
            else {
                var languagesList = string.Join(' ', LocalizationManager.Languages.Select(pair => $"`{pair.Key}`"));
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

        [Command("logging")]
        [Summary("logging0s")]
        public async Task LoggingControlPanel() {
            var botPermissions = (await Context.Guild.GetUserAsync(Program.Client.CurrentUser.Id)).GetPermissions((IGuildChannel) Context.Channel);
            if (botPermissions.SendMessages) {
                var loggingChainBase = LoggingChain.CreateInstance((ITextChannel) Context.Channel, Context.User, GuildConfig);
                await loggingChainBase.Start();
            }
            else {
                await (await Context.User.GetOrCreateDMChannelAsync()).SendMessageAsync(Loc.Get("ChainsCommon.CantSend").Format($"<#{Context.Channel.Id}>"));
            }
        }
    }
}