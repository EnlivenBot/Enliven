using System;
using System.Threading.Tasks;
using Bot.Utilities;
using Common;
using Common.Config;
using Common.Localization.Providers;
using Discord;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands.Modules {
    public class AdvancedModuleBase : PatchableModuleBase {
        private GuildLocalizationProvider? _loc;
        [DontInject] public GuildLocalizationProvider Loc => _loc ??= new GuildLocalizationProvider(GuildConfig);
        [DontInject] public GuildConfig GuildConfig { get; private set; } = null!;
        
        public IGuildConfigProvider GuildConfigProvider { get; set; } = null!;

        public async Task<IMessageChannel> GetResponseChannel(bool fileSupport = false) {
            var bot = (await Context.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel) Context.Channel);
            var user = (await Context.Guild.GetUserAsync(Context.User.Id)).GetPermissions((IGuildChannel) Context.Channel);
            return bot.SendMessages && (!fileSupport || bot.AttachFiles) && (!fileSupport || user.AttachFiles)
                ? Context.Channel
                : await Context.User.GetOrCreateDMChannelAsync();
        }

        protected override void BeforeExecute(CommandInfo command) {
            base.BeforeExecute(command);
            GuildConfig = GuildConfigProvider.Get(Context.Guild.Id);
        }

        protected override async Task<IUserMessage> ReplyAsync(string? message = null, bool isTTS = false, Embed? embed = null, RequestOptions? options = null, AllowedMentions? allowedMentions = null, MessageReference? messageReference = null) {
            return await (await GetResponseChannel()).SendMessageAsync(message, isTTS, embed, options).ConfigureAwait(false);
        }

        protected async Task<IUserMessage> ReplyFormattedAsync(string title, string description) {
            var embed = this.GetAuthorEmbedBuilder().WithTitle(title)
                            .WithDescription(description).WithColor(Color.Gold).Build();
            return await (await GetResponseChannel()).SendMessageAsync(null, false, embed).ConfigureAwait(false);
        }

        protected async Task<IUserMessage> ReplyFormattedAsync(string title, string description, TimeSpan delayedDeleteTime) {
            var replyFormattedAsync = ReplyFormattedAsync(title, description);
            replyFormattedAsync.DelayedDelete(delayedDeleteTime);
            return await replyFormattedAsync;
        }
    }
}