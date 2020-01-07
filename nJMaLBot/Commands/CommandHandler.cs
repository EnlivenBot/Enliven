using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands
{
    public class CommandHandler
    {
        public  CommandService      _cmds;
        public List<CommandInfo> AllCommands = new List<CommandInfo>();
        private DiscordSocketClient _client;

        public async Task Install(DiscordSocketClient c) {
            _client = c;
            _cmds = new CommandService();

            await _cmds.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            _cmds.AddTypeReader(typeof(ChannelFunction), new ChannelFunctionTypeReader());
            foreach (var cmdsModule in _cmds.Modules) {
                foreach (var command in cmdsModule.Commands) {
                    AllCommands.Add(command);
                }
            }

            _client.MessageReceived += HandleCommand;

            _client.UserJoined += AnnounceUserJoined;
            _client.UserLeft += AnnounceUserLeft;
        }

        public async Task AnnounceUserJoined(SocketGuildUser user) { }


        public async Task AnnounceUserLeft(SocketGuildUser user) { await Task.Delay(0); }
        public       void code()                                 { }

        public async Task HandleCommand(SocketMessage s) {
            var msg = s as SocketUserMessage;
            if (msg == null) return;

            var context = new CommandContext(_client, msg);
            if (!(s.Channel is SocketGuildChannel guildChannel)) return;

            var argPos = 0;
            if (msg.HasStringPrefix(GuildConfig.Get(guildChannel.Guild.Id).Prefix, ref argPos)) {
                var result = await _cmds.ExecuteAsync(context, argPos, null);

                if (!result.IsSuccess) {
                    switch (result.ToString()) {
                        default:

                            await s.Channel.SendMessageAsync(String.Format(Localization.Get(s.Channel.Id, "CommandHandler.Error"), result.ToString()));
                            break;
                        case "UnknownCommand: Unknown command.":

                            await msg.DeleteAsync();

                            await s.Channel.SendMessageAsync(String.Format(Localization.Get(s.Channel.Id, "CommandHandler.NotFound"), GuildConfig.Get(((SocketGuildChannel) s.Channel).Guild.Id).Prefix));
                            break;
                    }
                }
            }
        }
    }
}
