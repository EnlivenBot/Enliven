using System;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Config.Localization.Providers;
using Discord;
using Discord.Commands;

namespace Bot.Utilities.Commands {
    public class AdvancedModuleBase : ModuleBase {
        public async Task<IMessageChannel> GetResponseChannel(bool fileSupport = false) {
            var bot = (await Context.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel) Context.Channel);
            var user = (await Context.Guild.GetUserAsync(Context.User.Id)).GetPermissions((IGuildChannel) Context.Channel);
            return bot.SendMessages && (!fileSupport || bot.AttachFiles) && (!fileSupport || user.AttachFiles)
                ? Context.Channel : await Context.User.GetOrCreateDMChannelAsync();
        }

        private Lazy<GuildLocalizationProvider> _loc;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        protected override void BeforeExecute(CommandInfo command) {
            base.BeforeExecute(command);
            GuildConfig = GuildConfig.Get(Context.Guild.Id);
            _loc = new Lazy<GuildLocalizationProvider>(() => new GuildLocalizationProvider(GuildConfig));
        }

        public GuildLocalizationProvider Loc => _loc.Value;
        public GuildConfig GuildConfig { get; private set; }
    }
}