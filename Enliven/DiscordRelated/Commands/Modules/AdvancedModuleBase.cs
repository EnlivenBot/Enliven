using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions;
using Bot.Utilities;
using Common;
using Common.Config;
using Common.Localization.Providers;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using ModuleInfo = Discord.Interactions.ModuleInfo;

namespace Bot.DiscordRelated.Commands.Modules {
    public class AdvancedModuleBase : ModuleBase, IInteractionModuleBase {
        private GuildLocalizationProvider? _loc;
        private GuildConfig? _guildConfig;
        [DontInject] public GuildLocalizationProvider Loc => _loc ??= new GuildLocalizationProvider(GuildConfig);
        [DontInject] public GuildConfig GuildConfig => _guildConfig ??= GuildConfigProvider.Get(Context.Guild.Id);
        
        public IGuildConfigProvider GuildConfigProvider { get; set; } = null!;

        public async Task<IMessageChannel> GetResponseChannel(bool fileSupport = false) {
            var bot = (await Context.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel) Context.Channel);
            var user = (await Context.Guild.GetUserAsync(Context.User.Id)).GetPermissions((IGuildChannel) Context.Channel);
            return bot.SendMessages && (!fileSupport || bot.AttachFiles) && (!fileSupport || user.AttachFiles)
                ? Context.Channel
                : await Context.User.CreateDMChannelAsync();
        }

        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent component = null, ISticker[] stickers = null, Embed[] embeds = null) {
            return await (await GetResponseChannel()).SendMessageAsync(message, isTTS, embed, options, allowedMentions, messageReference, component, stickers, embeds).ConfigureAwait(false);
        }

        protected async Task<IUserMessage> ReplyFormattedAsync(string title, string description) {
            var embed = this.GetAuthorEmbedBuilder().WithTitle(title)
                            .WithDescription(description).WithColor(Color.Gold).Build();
            return await (await GetResponseChannel()).SendMessageAsync(null, false, embed).ConfigureAwait(false);
        }

        protected async Task<IUserMessage> ReplyFormattedAsync(string title, string description, TimeSpan delayedDeleteTime) {
            var message = await ReplyFormattedAsync(title, description);
            _ = message.DelayedDelete(delayedDeleteTime);
            return message;
        }

        public void SetContext(IInteractionContext context) {
            (this as IModuleBase).SetContext(new InteractionFallbackCommandContext(context));
        }
        public Task BeforeExecuteAsync(ICommandInfo command) => Task.CompletedTask;
        public void BeforeExecute(ICommandInfo command) { }
        public Task AfterExecuteAsync(ICommandInfo command) => Task.CompletedTask;
        public void AfterExecute(ICommandInfo command) { }
        public void OnModuleBuilding(InteractionService commandService, ModuleInfo module) { }
    }
}