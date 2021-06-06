using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Logging;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Localization;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    [Grouping("admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class AdminCommands : AdvancedModuleBase {
        public MessageHistoryService MessageHistoryService { get; set; } = null!;
        public GlobalBehaviorsService GlobalBehaviorsService { get; set; } = null!;
        public CommandHandlerService CommandHandlerService { get; set; } = null!;

        [Hidden]
        [Command("printwelcome")]
        public async Task PrintWelcome() {
            Context.Message.SafeDelete();
            (await GlobalBehaviorsService.PrintWelcomeMessage((SocketGuild) Context.Guild, Context.Channel)).DelayedDelete(Constants.LongTimeSpan);
        }
        
        [Command("setprefix")]
        [Summary("setprefix0s")]
        public async Task SetPrefix([Summary("setrefix0_0s")] string prefix) {
            GuildConfig.Prefix = prefix;
            GuildConfig.Save();
            await ReplyFormattedAsync(Loc.Get("Commands.Success"), Loc.Get("Commands.SetPrefixResponse").Format(prefix), Constants.LongTimeSpan);
            Context.Message.SafeDelete();
        }
        
        private async Task ListLanguages() {
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
                        var t = await CommandHandlerService.ExecuteCommand($"language {packName}", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                        if (t.IsSuccess) {
                            message.SafeDelete();
                            // ReSharper disable once AccessToModifiedClosure
                            collectors.DisposeAll();
                        }
                    }, CollectorFilter.IgnoreBots);
                }));
            try {
                await message.AddReactionsAsync(packsWithEmoji.Select(pair => pair.Value.LocalizationFlagEmoji).ToArray());
            }
            catch (Exception) {
                // ignored
            }
            message.DelayedDelete(Constants.StandardTimeSpan);
        }

        [Command("language")]
        [Alias("languages")]
        [Summary("language0s")]
        public async Task SetLanguage([Summary("language0_0s")] string? language = null) {
            if (language == null) {
                await ListLanguages();
                return;
            }

            if (LocalizationManager.Languages.ContainsKey(language)) {
                GuildConfig.SetLanguage(language).Save();
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
            GuildConfig.SetChannel(channel.Id.ToString(), func).Save();
            await ReplyFormattedAsync(Loc.Get("Commands.Success"), Loc.Get("Commands.SetChannelResponse").Format(channel.Id, func.ToString()));
            Context.Message.SafeDelete();
        }

        [Command("setchannel")]
        [Summary("setchannel0s")]
        public async Task SetThisChannel([Summary("setchannel0_0s")] ChannelFunction func) {
            await SetChannel(func, Context.Channel);
        }

        [Command("clearhistories", RunMode = RunMode.Async)]
        [Summary("clearhistories0s")]
        public async Task ClearHistories() {
            await ReplyAsync("Start clearing message histories");
            await MessageHistoryService.ClearGuildLogs((SocketGuild) Context.Guild);
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