using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization.Providers;
using Bot.Logging;
using Bot.Music;
using Bot.Music.Players;
using Bot.Utilities.Collector;
using Bot.Utilities.Emoji;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Tyrrrz.Extensions;

namespace Bot.Utilities.Commands {
    public class CommandHandler {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private IDiscordClient _client;

        private CommandHandler(IDiscordClient client, CommandService commandService) {
            _client = client;
            CommandService = commandService;
        }

        public CommandService CommandService { get; private set; }
        public List<CommandInfo> AllCommands { get; } = new List<CommandInfo>();
        public static FuzzySearch FuzzySearch { get; set; } = new FuzzySearch();

        public static async Task<CommandHandler> Create(DiscordShardedClient client) {
            var commandService = new CommandService();
            var commandHandler = new CommandHandler(client, commandService);
            logger.Info("Creating new command service");
            
            logger.Info("Adding modules");
            await commandService.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            commandService.AddTypeReader(typeof(ChannelFunction), new ChannelFunctionTypeReader());
            commandService.AddTypeReader(typeof(LoopingState), new LoopingStateTypeReader());
            commandService.AddTypeReader(typeof(BassBoostMode), new BassBoostModeTypeReader());
            foreach (var cmdsModule in commandService.Modules) {
                foreach (var command in cmdsModule.Commands) commandHandler.AllCommands.Add(command);
            }

            logger.Info("Adding commands to fuzzy search");
            foreach (var alias in commandHandler.AllCommands.SelectMany(commandInfo => commandInfo.Aliases).GroupBy(s => s).Select(grouping => grouping.First())) {
                FuzzySearch.AddData(alias);
            }
            
            Patch.ApplyCommandPatch();
            
            if (!Program.CmdOptions.Observer)
                client.MessageReceived += message => Task.Run(() => commandHandler.HandleCommand(message));

            return commandHandler;
        }

        private async Task HandleCommand(SocketMessage s) {
            if (!(s is SocketUserMessage msg)
             || msg.Source != MessageSource.User
             || !(s.Channel is SocketGuildChannel guildChannel)) {
                return;
            }

            var context = new CommandContext(_client, msg);
            var argPos = 0;
            var guild = GuildConfig.Get(guildChannel.Guild.Id);
            var loc = new GuildLocalizationProvider(guild);

            var hasStringPrefix = msg.HasStringPrefix(guild.Prefix, ref argPos);
            var hasMentionPrefix = HasMentionPrefix(msg, _client.CurrentUser, ref argPos);

            if (hasStringPrefix || hasMentionPrefix) {
                var query = msg.Content.Try(s1 => s1.Substring(argPos), "");
                if (string.IsNullOrEmpty(query)) query = " ";
                if (string.IsNullOrWhiteSpace(query) && hasMentionPrefix)
                    query = "help";
                var command = ParseCommand(query, out var args);

                var result = await ExecuteCommand(query, context, s.Author.Id.ToString());
                if (!result.IsSuccess && result.Error == CommandError.UnknownCommand) {
                    var searchResult = FuzzySearch.Search(command);
                    var bestMatch = searchResult.GetFullMatch();

                    // Check for a another keyboard layout
                    if (bestMatch != null) {
                        command = bestMatch.SimilarTo;
                        query = command + " " + args;
                        result = await ExecuteCommand(query, context, s.Author.Id.ToString());
                    }
                    else {
                        CollectorController? collector = null;
                        collector = CollectorsUtils.CollectReaction(msg, reaction => reaction.UserId == msg.Author.Id, async eventArgs => {
                            await eventArgs.RemoveReason();
                            // ReSharper disable once AccessToModifiedClosure
                            // ReSharper disable once PossibleNullReferenceException
                            collector?.Dispose();
                            try {
                                #pragma warning disable 4014
                                msg.RemoveReactionAsync(CommonEmoji.Help, Program.Client.CurrentUser);
                                #pragma warning restore 4014
                            }
                            catch {
                                // ignored
                            }

                            await SendErrorMessage(msg, loc, loc.Get("CommandHandler.UnknownCommand")
                                                                .Format(command.SafeSubstring(40, "..."),
                                                                     searchResult.GetBestMatches(3).Select(match => $"`{match.SimilarTo}`").JoinToString(", "),
                                                                     guild.Prefix));
                        });
                        await msg.AddReactionAsync(CommonEmoji.Help);
                        return;
                    }
                }

                if (!result.IsSuccess) {
                    switch (result.Error) {
                        case CommandError.ParseFailed:
                            await SendErrorMessage(msg, loc, string.Format(loc.Get("CommandHandler.ParseFailed"), guild.Prefix, command));
                            break;
                        case CommandError.BadArgCount:
                            await SendErrorMessage(msg, loc, string.Format(loc.Get("CommandHandler.BadArgCount"), guild.Prefix, command));
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

                    MessageHistoryManager.LogCreatedMessage(msg, guild);
                }
                else if (guild.IsCommandLoggingEnabled) {
                    MessageHistoryManager.LogCreatedMessage(msg, guild);
                }
            }
            else
                MessageHistoryManager.LogCreatedMessage(s, guild);
        }

        public async Task<IResult> ExecuteCommand(string query, ICommandContext context, string authorId) {
            var result = await CommandService.ExecuteAsync(context, query, null);
            if (result.Error != CommandError.UnknownCommand) {
                var commandName = query.IndexOf(" ", StringComparison.Ordinal) > -1 ? query.Substring(0, query.IndexOf(" ", StringComparison.Ordinal)) : query;
                RegisterUsage(commandName, authorId);
                RegisterUsage(commandName, "Global");
            }

            return result;
        }

        private static async Task SendErrorMessage(SocketUserMessage message, ILocalizationProvider loc, string description) {
            (await message.Channel.SendMessageAsync(null, false, BuildErrorEmbed(message, loc, description))).DelayedDelete(TimeSpan.FromMinutes(10));
        }

        private static Embed BuildErrorEmbed(SocketUserMessage message, ILocalizationProvider loc, string description) {
            var builder = new EmbedBuilder();
            builder.WithFooter(loc.Get("Commands.RequestedBy").Format(message.Author.Username), message.Author.GetAvatarUrl())
                   .WithColor(Color.Orange);
            builder.WithTitle(loc.Get("CommandHandler.FailedTitle"))
                   .WithDescription(description);
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

            userStatistics.UsagesList["PlaybackTime"] = (int) (userUsageCount + span.TotalSeconds);
            GlobalDB.CommandStatistics.Upsert(userStatistics);
        }

        public static TimeSpan GetTotalMusicTime() {
            var userStatistics = GlobalDB.CommandStatistics.FindById("Music") ?? new StatisticsPart {Id = "Music"};
            if (!userStatistics.UsagesList.TryGetValue("PlaybackTime", out var userUsageCount)) {
                userUsageCount = 0;
            }

            return TimeSpan.FromSeconds(userUsageCount);
        }

        private static string ParseCommand(string input, out string args) {
            var command = input.Substring(0, input.IndexOf(" ", StringComparison.Ordinal) > 0 ? input.IndexOf(" ", StringComparison.Ordinal) : input.Length);
            try {
                args = input.Substring(command.Length + 1);
            }
            catch {
                args = "";
            }

            return command;
        }
    }
}