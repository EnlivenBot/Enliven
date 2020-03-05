using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Config.Localization.Providers;
using Bot.Music.Players;
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
            _commands.AddTypeReader(typeof(LoopingState), new LoopingStateTypeReader());
            foreach (var cmdsModule in _commands.Modules) {
                foreach (var command in cmdsModule.Commands) AllCommands.Add(command);
            }

            _client.MessageReceived += HandleCommand;
        }

        private async Task HandleCommand(SocketMessage s) {
            if (!(s is SocketUserMessage msg) || msg.Source != MessageSource.User)
                return;
            var context = new CommandContext(_client, msg);
            if (!(s.Channel is SocketGuildChannel guildChannel)) return;

            var argPos = 0;
            var guild = GuildConfig.Get(guildChannel.Guild.Id);
            var loc = new GuildLocalizationProvider(guild);
            if (msg.HasStringPrefix(guild.Prefix, ref argPos) || HasMentionPrefix(msg, _client.CurrentUser, ref argPos)) {
                var query = msg.Content.SafeSubstring(argPos, 800);
                if (string.IsNullOrWhiteSpace(query))
                    query = "help";
                var result = await _commands.ExecuteAsync(context, query, null);
                if (!result.IsSuccess) {
                    switch (result.Error) {
                        case CommandError.UnknownCommand:
                            await SendErrorMessage(msg, loc, string.Format(loc.Get("CommandHandler.UnknownCommand"),
                                query, guild.Prefix));
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
                        case CommandError.ObjectNotFound:
                            await SendErrorMessage(msg, loc, result.ErrorReason);
                            break;
                        case CommandError.MultipleMatches:
                            await SendErrorMessage(msg, loc, result.ErrorReason);
                            break;
                    }

                    MessageHistoryManager.StartLogToHistory(s, guild);
                    msg.SafeDelete();
                }
                else if (guild.IsCommandLoggingEnabled)
                    MessageHistoryManager.StartLogToHistory(s, guild);
                else
                    MessageHistoryManager.AddMessageToIgnore(s);
            }
            else
                MessageHistoryManager.StartLogToHistory(s, guild);
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

        private static bool HasMentionPrefix(IUserMessage msg, IUser user, ref int argPos) {
            var content = msg.Content;
            if (string.IsNullOrEmpty(content) || content.Length <= 3 || (content[0] != '<' || content[1] != '@'))
                return false;
            var num = content.IndexOf('>');
            if (num == -1 || !MentionUtils.TryParseUser(content.Substring(0, num + 1), out var userId) ||
                (long) userId != (long) user.Id)
                return false;
            argPos = num + 2;
            return true;
        }
    }
}