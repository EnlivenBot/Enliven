using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET;
using Microsoft.Extensions.DependencyInjection;

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
        }

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
                                string.Format(Localization.Get(guildChannel.Guild.Id, "CommandHandler.Error"),
                                    result));
                            break;
                        case "UnknownCommand: Unknown command.":
                            try {
                                await msg.DeleteAsync();
                            }
                            catch (Exception) { }

                            await s.Channel.SendMessageAsync(
                                        string.Format(Localization.Get(guildChannel.Guild.Id, "CommandHandler.NotFound"),
                                            GuildConfig.Get(guildChannel.Guild.Id).Prefix));
                            break;
                    }
            }
        }
    }
}