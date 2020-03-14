using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public CommandService CommandService { get; private set; }
        public List<CommandInfo> AllCommands { get; } = new List<CommandInfo>();

        public async Task Install(DiscordSocketClient c) {
            _client = c;
            CommandService = new CommandService();

            await CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            CommandService.AddTypeReader(typeof(ChannelFunction), new ChannelFunctionTypeReader());
            CommandService.AddTypeReader(typeof(LoopingState), new LoopingStateTypeReader());
            foreach (var cmdsModule in CommandService.Modules) {
                foreach (var command in cmdsModule.Commands) AllCommands.Add(command);
            }

            _client.MessageReceived += HandleCommand;
        }

        private async Task HandleCommand(SocketMessage s) {
            if (!(s is SocketUserMessage msg) || msg.Source != MessageSource.User) {
                MessageHistoryManager.AddMessageToIgnore(s);
                return;
            }
            var context = new CommandContext(_client, msg);
            if (!(s.Channel is SocketGuildChannel guildChannel)) {
                MessageHistoryManager.AddMessageToIgnore(s);
                return;
            }

            var argPos = 0;
            var guild = GuildConfig.Get(guildChannel.Guild.Id);
            var loc = new GuildLocalizationProvider(guild);
            if (msg.HasStringPrefix(guild.Prefix, ref argPos) || HasMentionPrefix(msg, _client.CurrentUser, ref argPos)) {
                var query = msg.Content.SafeSubstring(argPos, 800);
                if (string.IsNullOrWhiteSpace(query))
                    query = "help";

                var result = await ExecuteCommand(query, context, s.Author.Id.ToString());
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
                else {
                    
                    if (guild.IsCommandLoggingEnabled)
                        MessageHistoryManager.StartLogToHistory(s, guild);
                    else
                        MessageHistoryManager.AddMessageToIgnore(s);
                }
            }
            else
                MessageHistoryManager.StartLogToHistory(s, guild);
        }

        public Task<IResult> ExecuteCommand(string query, ICommandContext context, string authorId) {
            var result = CommandService.ExecuteAsync(context, query, null);
            var commandName = query.IndexOf(" ") > -1 ? query.Substring(0, query.IndexOf(" ")) : query;
            RegisterUsage(commandName, authorId);
            RegisterUsage(commandName, "Global");
            return result;
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

        public static void RegisterUsage(string command, string userId) {
            var userStatistics = GlobalDB.CommandStatistics.FindById(userId) ?? new StatisticsPart {Id = userId};
            if (!userStatistics.UsagesList.TryGetValue(command, out var userUsageCount)) {
                userUsageCount = 0;
            }

            userStatistics.UsagesList[command] = ++userUsageCount;
            GlobalDB.CommandStatistics.Upsert(userStatistics);
        }
        
        public static void RegisterMusicTime(TimeSpan span) {
            var userStatistics = GlobalDB.CommandStatistics.FindById("Music") ?? new StatisticsPart {Id = "Music"};
            if (!userStatistics.UsagesList.TryGetValue("PlaybackTime", out var userUsageCount)) {
                userUsageCount = 0;
            }
            
            userStatistics.UsagesList["PlaybackTime"] = (ulong) (userUsageCount + span.TotalSeconds);
            GlobalDB.CommandStatistics.Upsert(userStatistics);
        }

        public static TimeSpan GetTotalMusicTime() {
            var userStatistics = GlobalDB.CommandStatistics.FindById("Music") ?? new StatisticsPart {Id = "Music"};
            if (!userStatistics.UsagesList.TryGetValue("PlaybackTime", out var userUsageCount)) {
                userUsageCount = 0;
            }
            
            return TimeSpan.FromSeconds(userUsageCount);
        }
    }
}