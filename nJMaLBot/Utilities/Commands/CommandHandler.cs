using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;

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
            var guild = GuildConfig.Get(guildChannel.Guild.Id);
            var loc = new GuildLocalizationProvider(guild);
            if (msg.HasStringPrefix(guild.Prefix, ref argPos)) {
                var result = await _commands.ExecuteAsync(context, argPos, null);
                if (!result.IsSuccess) {
                    // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                    switch (result.Error) {
                        case CommandError.UnknownCommand:
                            await SendErrorMessage(msg, loc, string.Format(loc.Get("CommandHandler.UnknownCommand"),
                                Regex.Match(s.Content, $@"(?<={guild.Prefix})[a-z]{{2,}}(?= )?").Value,
                                guild.Prefix));
                            break;
                        case CommandError.ParseFailed:
                            await SendErrorMessage(msg, loc, string.Format(loc.Get("CommandHandler.ParseFailed"),
                                guild.Prefix,
                                Regex.Match(s.Content, $@"(?<={guild.Prefix})[a-z]{{2,}}(?= )?").Value));
                            break;
                        case CommandError.BadArgCount:
                            await SendErrorMessage(msg, loc, string.Format(loc.Get("CommandHandler.BadArgCount"),
                                guild.Prefix,
                                Regex.Match(s.Content, $@"(?<={guild.Prefix})[a-z]{{2,}}(?= )?").Value));
                            break;
                        case CommandError.UnmetPrecondition:
                            await SendErrorMessage(msg, loc, loc.Get("CommandHandler.UnmetPrecondition"));
                            break;
                        case CommandError.Exception:
                            await SendErrorMessage(msg, loc, string.Format(loc.Get("CommandHandler.Exception"), result));
                            break;
                    }
                    msg.SafeDelete();
                }
            }
        }

        private static async Task SendErrorMessage(SocketUserMessage message, ILocalizationProvider loc, string description) {
            (await message.Channel.SendMessageAsync(null, false, BuildErrorEmbed(message, loc, description))).DelayedDelete(TimeSpan.FromMinutes(10));
        }

        private static Embed BuildErrorEmbed(SocketUserMessage message, ILocalizationProvider loc, string description) {
            var builder = new EmbedBuilder();
            builder.WithAuthor(message.Author.Username, message.Author.GetAvatarUrl());
            builder.WithTitle(loc.Get("CommandHandler.FailedTitle"));
            builder.WithDescription(description);
            return builder.Build();
        }
    }
}