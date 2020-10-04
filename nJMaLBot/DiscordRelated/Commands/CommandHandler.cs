using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Emoji;
using Bot.Config.Localization.Entries;
using Bot.Config.Localization.Providers;
using Bot.DiscordRelated.Logging;
using Bot.DiscordRelated.Music;
using Bot.Music;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.Commands {
    public class CommandHandler {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private IDiscordClient _client;

        private CommandHandler(IDiscordClient client, CustomCommandService commandService) {
            _client = client;
            CommandService = commandService;
        }

        public CustomCommandService CommandService { get; private set; }
        public static FuzzySearch FuzzySearch { get; set; } = new FuzzySearch();
        public List<CommandInfo> AllCommands { get; } = new List<CommandInfo>();
        public Lookup<string, CommandInfo> CommandAliases { get; set; } = null!;

        public static async Task<CommandHandler> Create(DiscordShardedClient client) {
            var commandService = new CustomCommandService();
            var commandHandler = new CommandHandler(client, commandService);
            logger.Info("Creating new query service");

            logger.Info("Adding modules");
            await commandService.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            commandService.AddTypeReader(typeof(ChannelFunction), new ChannelFunctionTypeReader());
            commandService.AddTypeReader(typeof(LoopingState), new LoopingStateTypeReader());
            commandService.AddTypeReader(typeof(BassBoostMode), new BassBoostModeTypeReader());
            foreach (var cmdsModule in commandService.Modules) {
                foreach (var command in cmdsModule.Commands) commandHandler.AllCommands.Add(command);
            }

            var items = new List<KeyValuePair<string, CommandInfo>>();
            foreach (var command in commandHandler.AllCommands) {
                items.AddRange(command.Aliases.Select(alias => new KeyValuePair<string, CommandInfo>(alias, command)));
            }

            commandHandler.CommandAliases = (Lookup<string, CommandInfo>) items.ToLookup(pair => pair.Key, pair => pair.Value);

            logger.Info("Adding commands to fuzzy search");
            foreach (var alias in commandHandler.AllCommands.SelectMany(commandInfo => commandInfo.Aliases).GroupBy(s => s)
                                                .Select(grouping => grouping.First())) {
                FuzzySearch.AddData(alias);
            }

            Patch.ApplyCommandPatch();

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

            bool isCommand = false;
            if (hasStringPrefix || hasMentionPrefix) {
                isCommand = true;
                var query = msg.Content.Try(s1 => s1.Substring(argPos), "");
                if (string.IsNullOrEmpty(query)) query = " ";
                if (string.IsNullOrWhiteSpace(query) && hasMentionPrefix) query = "help";

                var command = await GetCommand(query, context);

                if (command.Item1 == null) {
                    if (command.Item2.Error == CommandError.UnmetPrecondition) {
                        await SendErrorMessage(msg, loc, loc.Get("CommandHandler.UnmetPrecondition"));
                    }
                    else {
                        await AddEmojiErrorHint(msg, loc, CommonEmoji.Help,
                            new EntryLocalized("CommandHandler.UnknownCommand").Add(query.SafeSubstring(40, "...")!,
                                FuzzySearch.Search(query).GetBestMatches(3).Select(match => $"`{match.SimilarTo}`").JoinToString(", "),
                                guild.Prefix));
                    }

                    return;
                }

                var result = command.Item1.Value.Value.IsSuccess
                    ? await ExecuteCommand(msg, query, context, command.Item1.Value, s.Author.Id.ToString())
                    : command.Item1.Value.Value;

                if (!result.IsSuccess) {
                    switch (result.Error) {
                        case CommandError.ParseFailed:
                            await AddEmojiErrorHint(msg, loc, CommonEmoji.Help, new EntryLocalized("CommandHandler.ParseFailed"),
                                HelpUtils.BuildHelpFields(command.Item1.Value.Key.Alias, guild.Prefix, loc));
                            break;
                        case CommandError.BadArgCount:
                            await AddEmojiErrorHint(msg, loc, CommonEmoji.Help, new EntryLocalized("CommandHandler.BadArgCount"),
                                HelpUtils.BuildHelpFields(command.Item1.Value.Key.Alias, guild.Prefix, loc));
                            break;
                        case CommandError.ObjectNotFound:
                            await SendErrorMessage(msg, loc, result.ErrorReason);
                            break;
                        case CommandError.MultipleMatches:
                            await SendErrorMessage(msg, loc, result.ErrorReason);
                            break;
                    }
                }
            }

            MessageHistoryManager.TryLogCreatedMessage(s, guild, isCommand);
        }

        private static async Task AddEmojiErrorHint(SocketUserMessage targetMessage, ILocalizationProvider loc, IEmote emote, IEntry description,
                                                    IEnumerable<EmbedFieldBuilder>? builders = null) {
            CollectorController? collector = null;
            collector = CollectorsUtils.CollectReaction(targetMessage, reaction => reaction.UserId == targetMessage.Author.Id, async eventArgs => {
                await eventArgs.RemoveReason();
                // ReSharper disable once AccessToModifiedClosure
                // ReSharper disable once PossibleNullReferenceException
                collector?.Dispose();
                try {
                    #pragma warning disable 4014
                    targetMessage.RemoveReactionAsync(CommonEmoji.Help, Program.Client.CurrentUser);
                    #pragma warning restore 4014
                }
                catch {
                    // ignored
                }

                await SendErrorMessage(targetMessage, loc, description.Get(loc), builders);
            });
            try {
                await targetMessage.AddReactionAsync(emote);
            }
            catch (Exception) {
                collector.Dispose();
            }
        }

        private async Task<(KeyValuePair<CommandMatch, ParseResult>?, IResult)> GetCommand(string query, ICommandContext context) {
            var (commandMatch, result) = await CommandService.FindAsync(context, query, null);
            if (result.IsSuccess) return (commandMatch, result);

            if (result.Error != CommandError.UnknownCommand) return (commandMatch, result);
            string args;
            var command = query.Substring(0, query.IndexOf(" ", StringComparison.Ordinal) > 0 ? query.IndexOf(" ", StringComparison.Ordinal) : query.Length);
            try {
                args = query.Substring(command.Length + 1);
            }
            catch {
                args = "";
            }

            var searchResult = FuzzySearch.Search(command);
            var bestMatch = searchResult.GetFullMatch();

            // Check for a another keyboard layout
            if (bestMatch == null) return (commandMatch, result);

            command = bestMatch.SimilarTo;
            query = $"{command} {args}";
            var (commandMatch2, result2) = await CommandService.FindAsync(context, query, null);
            return result2.IsSuccess ? (commandMatch2, result2) : (commandMatch, result);
        }

        public async Task<IResult> ExecuteCommand(IMessage message, string query, ICommandContext context, KeyValuePair<CommandMatch, ParseResult> pair,
                                                  string authorId) {
            IResult result = CollectorsUtils.OnCommandExecute(pair, context, message)
                ? await pair.Key.ExecuteAsync(context, pair.Value, EmptyServiceProvider.Instance)
                : ExecuteResult.FromSuccess();

            if (result.Error != CommandError.UnknownCommand) {
                var commandName = query.IndexOf(" ", StringComparison.Ordinal) > -1 ? query.Substring(0, query.IndexOf(" ", StringComparison.Ordinal)) : query;
                RegisterUsage(commandName, authorId);
                RegisterUsage(commandName, "Global");
            }

            return result;
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

        private static async Task SendErrorMessage(SocketUserMessage message, ILocalizationProvider loc, string description,
                                                   IEnumerable<EmbedFieldBuilder>? fieldBuilders) {
            if (fieldBuilders == null) {
                await SendErrorMessage(message, loc, description);
                return;
            }

            (await message.Channel.SendMessageAsync(null, false, GetErrorEmbed(message.Author, loc, description).WithFields(fieldBuilders).Build()))
               .DelayedDelete(
                    Constants.LongTimeSpan);
        }

        private static async Task SendErrorMessage(SocketUserMessage message, ILocalizationProvider loc, string description) {
            (await message.Channel.SendMessageAsync(null, false, GetErrorEmbed(message.Author, loc, description).Build()))
               .DelayedDelete(Constants.LongTimeSpan);
        }

        public static EmbedBuilder GetErrorEmbed(IUser user, ILocalizationProvider loc, string description) {
            var builder = new EmbedBuilder();
            builder.WithFooter(loc.Get("Commands.RequestedBy").Format(user.Username), user.GetAvatarUrl())
                   .WithColor(Color.Orange);
            builder.WithTitle(loc.Get("CommandHandler.FailedTitle"))
                   .WithDescription(description);
            return builder;
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

        public CommandInfo GetCommandByName(string commandName) {
            return CommandAliases[commandName].First();
        }
    }
}