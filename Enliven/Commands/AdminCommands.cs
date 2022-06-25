using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageComponents;
using Bot.Utilities;
using Common;
using Common.Config;
using Common.Localization;
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
            Context.Message.SafeDelete();
            _ = GlobalBehaviorsService.PrintWelcomeMessage((SocketGuild) Context.Guild, Context.Channel).DelayedDelete(Constants.LongTimeSpan);
        }
        
        [SlashCommandAdapter(false)]
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
            var componentBuilder = MessageComponentService.GetBuilder();
            var i = 0;
            foreach (var (key, pack) in LocalizationManager.Languages) {
                embedBuilder.AddField($"{pack.LocalizationFlagEmojiText} **{pack.LocalizedName}** ({pack.LanguageName})",
                    Loc.Get("Localization.LanguageDescription").Format(GuildConfig.Prefix, key, pack.Authors, pack.TranslationCompleteness), true);
                var enlivenButtonBuilder = new EnlivenButtonBuilder().WithStyle(ButtonStyle.Secondary)
                    .WithEmote(pack.LocalizationFlagEmoji!).WithLabel($"{pack.LocalizedName} ({pack.LanguageName})")
                    .WithCustomId(key).WithTargetRow(i++ / 5);
                componentBuilder.WithButton(enlivenButtonBuilder);
            }

            var message = await ReplyAsync(embed: embedBuilder.Build(), component: componentBuilder.Build());
            componentBuilder.AssociateWithMessage(message);
            componentBuilder.SetCallback(async (s, component, arg3) => {
                var t = await CommandHandlerService.ExecuteCommand($"language {s}", new ComponentCommandContext(Context.Client, component),
                    component.User.Id.ToString());
                if (t.IsSuccess) message.SafeDelete();
            });
            _ = message.DelayedDelete(Constants.StandardTimeSpan).ContinueWith(task => componentBuilder.Dispose());
        }

        [Command("language")]
        [Alias("languages")]
        [Summary("language0s")]
        public async Task SetLanguage([Summary("language0_0s")] string? language = null) {
            Context.Message.SafeDelete();
            if (language == null) {
                await ListLanguages();
                return;
            }

            if (LocalizationManager.Languages.ContainsKey(language)) {
                GuildConfig.SetLanguage(language).Save();
                await ReplyFormattedAsync(Loc.Get("Commands.Success"), Loc.Get("Localization.Success").Format(language), TimeSpan.FromMinutes(1));
            }
            else {
                var languagesList = string.Join(' ', LocalizationManager.Languages.Select(pair => $"`{pair.Key}`"));
                await ReplyFormattedAsync(Loc.Get("Commands.Fail"), Loc.Get("Localization.Fail").Format(language, languagesList), TimeSpan.FromMinutes(1));
            }
        }

        [Command("setchannelrole")]
        [Summary("setchannelrole0s")]
        public async Task SetChannelRole([Summary("setchannelrole0_0s")] ChannelFunction func,
                                         [Summary("setchannelrole0_1s")] IChannel? channel = null) {
            if (channel is ICategoryChannel) {
                await ReplyFormattedAsync(Loc.Get("Commands.Fail"), Loc.Get("Commands.CategoryChannelNotSupported"));
                return;
            }
            if (channel is IVoiceChannel) {
                await ReplyFormattedAsync(Loc.Get("Commands.Fail"), Loc.Get("Commands.VoiceChannelNotSupported"));
                return;
            }
            GuildConfig.SetChannel(func, channel?.Id).Save();
            var description = channel != null 
                ? Loc.Get("Commands.SetChannelRoleResponse", channel.Id, func)
                : Loc.Get("Commands.ClearChannelRoleResponse", func);
            await ReplyFormattedAsync(Loc.Get("Commands.Success"), description);
            Context.Message?.SafeDelete();
        }
    }
}