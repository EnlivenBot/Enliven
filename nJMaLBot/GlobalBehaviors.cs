using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities;
using Discord;
using Discord.WebSocket;

namespace Bot {
    public static class GlobalBehaviors {
        static GlobalBehaviors() {
            Program.Client.JoinedGuild += ClientOnJoinedGuild;
        }

        private static async Task ClientOnJoinedGuild(SocketGuild arg) {
            GuildConfig.TryCreate(arg.Id, false);
            await PrintWelcomeMessage(arg);
        }

        public static async Task<IUserMessage> PrintWelcomeMessage(SocketGuild guild, IMessageChannel? channel = null) {
            var guildConfig = GuildConfig.Get(guild.Id);
            var loc = guildConfig.Loc;

            var embedBuilder = new EmbedBuilder().WithColor(Color.Gold).WithFooter($"Powered by {Program.Client.CurrentUser.Username}");
            embedBuilder.WithAuthor(Program.Client.CurrentUser.Username, Program.Client.CurrentUser.GetAvatarUrl());
            embedBuilder.WithDescription(loc.Get("Messages.WelcomeDescription").Format(guildConfig.Prefix, Program.Client.CurrentUser.Mention));
            embedBuilder.AddField(loc.Get("Messages.WelcomeMusicTitle"), loc.Get("Messages.WelcomeMusic").Format(guildConfig.Prefix));
            embedBuilder.AddField(loc.Get("Messages.WelcomeLoggingTitle"), loc.Get("Messages.WelcomeLogging").Format(guildConfig.Prefix));
            embedBuilder.AddField(loc.Get("Messages.WelcomeLocalizationTitle"), loc.Get("Messages.WelcomeLocalization").Format(guildConfig.Prefix));
            embedBuilder.AddField(loc.Get("Messages.WelcomeInfoTitle"), loc.Get("Messages.WelcomeInfo").Format(guildConfig.Prefix));
            embedBuilder.AddField(loc.Get("Messages.WelcomeGithubTitle"), loc.Get("Messages.WelcomeGithub"));
            return await (channel ?? guild.DefaultChannel).SendMessageAsync(null, false, embedBuilder.Build());
        }

        public static void Initialize() {
            // Dummy method to call static constructor
        }
    }
}