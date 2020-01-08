using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    public class CommandHandler {
        private DiscordSocketClient _client;
        private CommandService _commands;
        public List<CommandInfo> AllCommands { get; } = new List<CommandInfo>();

        public async Task Install(DiscordSocketClient c) {
            _client = c;
            _commands = new CommandService();

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            _commands.AddTypeReader(typeof(ChannelFunction), new ChannelFunctionTypeReader());
            foreach (var cmdsModule in _commands.Modules) {
                foreach (var command in cmdsModule.Commands) AllCommands.Add(command);
            }

            _client.MessageReceived += HandleCommand;

            _client.UserJoined += AnnounceUserJoined;
            _client.UserLeft += AnnounceUserLeft;
        }

        public async Task AnnounceUserJoined(SocketGuildUser user) { }


        public async Task AnnounceUserLeft(SocketGuildUser user) {
            await Task.Delay(0);
        }

        public void code() { }

        private async Task HandleCommand(SocketMessage s) {
            if (!(s is SocketUserMessage msg))
                return;

            var context = new CommandContext(_client, msg);
            if (!(s.Channel is SocketGuildChannel guildChannel)) return;

            var argPos = 0;
            if (msg.HasStringPrefix(GuildConfig.Get(guildChannel.Guild.Id).Prefix, ref argPos)) {
                var result = await _commands.ExecuteAsync(context, argPos, null);

                if (!result.IsSuccess)
                    switch (result.ToString()) {
                        default:

                            await s.Channel.SendMessageAsync(
                                string.Format(Localization.Get(s.Channel.Id, "CommandHandler.Error"),
                                    result));
                            break;
                        case "UnknownCommand: Unknown command.":

                            await msg.DeleteAsync();

                            await s.Channel.SendMessageAsync(
                                string.Format(Localization.Get(s.Channel.Id, "CommandHandler.NotFound"),
                                    GuildConfig.Get(guildChannel.Guild.Id).Prefix));
                            break;
                    }
            }
        }
    }
}