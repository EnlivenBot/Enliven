using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Commands.Modules.Contexts;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageComponents;
using Bot.Utilities;
using Common;
using Common.Config;
using Common.Localization;
using Common.Localization.Entries;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    [SlashCommandAdapter]
    [Grouping("admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class AdminCommands : AdvancedModuleBase {
        public GlobalBehaviorsService GlobalBehaviorsService { get; set; } = null!;
        public CommandHandlerService CommandHandlerService { get; set; } = null!;
        public MessageComponentService MessageComponentService { get; set; } = null!;

        [Hidden]
        [Command("printwelcome")]
        public async Task PrintWelcome() {
            // TODO: Refactor using GlobalBehaviorsService
            await GlobalBehaviorsService.PrintWelcomeMessage((SocketGuild) Context.Guild, Context.Channel).DelayedDelete(Constants.LongTimeSpan);
            await this.RemoveMessageInvokerIfPossible();
        }
        
        [SlashCommandAdapter(false)]
        [Command("setprefix")]
        [Summary("setprefix0s")]
        public async Task SetPrefix([Summary("setrefix0_0s")] string prefix) {
            GuildConfig.Prefix = prefix;
            GuildConfig.Save();
            await this.ReplySuccessFormattedAsync(new EntryLocalized("Commands.SetPrefixResponse", prefix)).CleanupAfter(Constants.LongTimeSpan);
            await this.RemoveMessageInvokerIfPossible();
        }
        
        private async Task ListLanguages() {
            // TODO: Rework language selection with popups
            var embedBuilder = this.GetAuthorEmbedBuilder().WithColor(Color.Gold).WithTitle(Loc.Get("Localization.LanguagesList"));
            var componentBuilder = MessageComponentService.GetBuilder();
            var i = 0;
            foreach (var (key, pack) in LocalizationManager.Languages) {
                embedBuilder.AddField($"{pack.LocalizationFlagEmojiText} **{pack.LocalizedName}** ({pack.LanguageName})",
                    Loc.Get("Localization.LanguageDescription").Format(key, pack.Authors, pack.TranslationCompleteness), true);
                var enlivenButtonBuilder = new EnlivenButtonBuilder().WithStyle(ButtonStyle.Secondary)
                    .WithEmote(pack.LocalizationFlagEmoji!).WithLabel($"{pack.LocalizedName} ({pack.LanguageName})")
                    .WithCustomId(key).WithTargetRow(i++ / 5);
                componentBuilder.WithButton(enlivenButtonBuilder);
            }

            var sentMessage = await Context.SendMessageAsync(null, embedBuilder.Build(), components: componentBuilder.Build());
            componentBuilder.AssociateWithMessage(sentMessage.GetMessageAsync());
            componentBuilder.SetCallback(async (s, component, arg3) => {
                var t = await CommandHandlerService.ExecuteCommand($"language {s}", new ComponentCommandContext(Context.Client, component),
                    component.User.Id.ToString());
                if (t.IsSuccess) await sentMessage.GetMessageAsync().PipeAsync(userMessage => userMessage.SafeDeleteAsync());
                componentBuilder.Dispose();
            });
            _ = sentMessage.CleanupAfterAsync(Constants.StandardTimeSpan).ContinueWith(task => componentBuilder.Dispose());
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
                await this.ReplySuccessFormattedAsync(new EntryLocalized("Localization.Success", language), true).CleanupAfter(Constants.StandardTimeSpan);
            }
            else {
                var languagesList = string.Join(' ', LocalizationManager.Languages.Select(pair => $"`{pair.Key}`"));
                await this.ReplyFailFormattedAsync(new EntryLocalized("Localization.Fail", language, languagesList), true).CleanupAfter(Constants.ShortTimeSpan);
            }
            await this.RemoveMessageInvokerIfPossible();
        }

        [Command("setchannelrole")]
        [Summary("setchannelrole0s")]
        public async Task SetChannelRole([Summary("setchannelrole0_0s")] ChannelFunction func,
                                         [Summary("setchannelrole0_1s")] IChannel? channel = null) {
            if (channel is ICategoryChannel) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Commands.CategoryChannelNotSupported"));
                return;
            }
            if (channel is IVoiceChannel) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Commands.VoiceChannelNotSupported"));
                return;
            }
            GuildConfig.SetChannel(func, channel?.Id).Save();
            var description = channel != null 
                ? new EntryLocalized("Commands.SetChannelRoleResponse", channel.Id, func)
                : new EntryLocalized("Commands.ClearChannelRoleResponse", func);
            await this.ReplySuccessFormattedAsync(description);
            await this.RemoveMessageInvokerIfPossible();
        }
    }
}