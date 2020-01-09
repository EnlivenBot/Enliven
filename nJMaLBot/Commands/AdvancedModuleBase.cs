using System;
using System.Threading.Tasks;
using Bot.Config;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    public class AdvancedModuleBase : ModuleBase {
        public async Task<IMessageChannel> GetResponseChannel(bool fileSupport = false) {
            var bot = (await Context.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel) Context.Channel);
            var user = (await Context.Guild.GetUserAsync(Context.User.Id)).GetPermissions((IGuildChannel) Context.Channel);
            return bot.SendMessages && (!fileSupport || bot.AttachFiles) && (!fileSupport || user.AttachFiles)
                ? Context.Channel
                : await Context.User.GetOrCreateDMChannelAsync();
        }

        private Lazy<LocalizationProvider> _loc;

        protected override void BeforeExecute(CommandInfo command) {
            base.BeforeExecute(command);
            GuildConfig = GuildConfig.Get(Context.Guild.Id);
            _loc = new Lazy<LocalizationProvider>(() => new LocalizationProvider(GuildConfig));
        }

        public LocalizationProvider Loc => _loc.Value;
        public GuildConfig GuildConfig { get; private set; }
    }
}