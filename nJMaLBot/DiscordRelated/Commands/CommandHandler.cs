using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Emoji;
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

            bool isCommand = false;
            if (hasStringPrefix || hasMentionPrefix) {
                isCommand = true;
                var query = msg.Content.Try(s1 => s1.Substring(argPos), "");
                if (string.IsNullOrEmpty(query)) query = " ";
                if (string.IsNullOrWhiteSpace(query) && hasMentionPrefix) query = "help";

                var command = await GetCommand(query, context);

                if (command == null) {
                    await OnCommandNotFound(msg, loc, query, guild);
                    return;
                }

                var result = command.Value.Value.IsSuccess
                    ? await ExecuteCommand(msg, query, context, command.Value, s.Author.Id.ToString())
                    : command.Value.Value;

                if (!result.IsSuccess) {
                    switch (result.Error) {
                        case CommandError.UnknownCommand:
                            await OnCommandNotFound(msg, loc, query, guild);
                            return;
                        case CommandError.ParseFailed:
                            await SendErrorMessage(msg, loc, loc.Get("CommandHandler.ParseFailed", guild.Prefix, command.Value.Key.Alias));
                            break;
                        case CommandError.BadArgCount:
                            await SendErrorMessage(msg, loc, loc.Get("CommandHandler.BadArgCount", guild.Prefix, command.Value.Key.Alias));
                            break;
                        case CommandError.UnmetPrecondition:
                            await SendErrorMessage(msg, loc, loc.Get("CommandHandler.UnmetPrecondition"));
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

        private static async Task OnCommandNotFound(SocketUserMessage msg, GuildLocalizationProvider loc, string query, GuildConfig guild) {
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
                                                    .Format(query.SafeSubstring(40, "..."),
                                                         FuzzySearch.Search(query).GetBestMatches(3).Select(match => $"`{match.SimilarTo}`").JoinToString(", "),
                                                         guild.Prefix));
            });
            await msg.AddReactionAsync(CommonEmoji.Help);
        }

        private async Task<KeyValuePair<CommandMatch, ParseResult>?> GetCommand(string query, ICommandContext context) {
            var (commandMatch, result) = await CommandService.FindAsync(context, query, null);
            if (result.IsSuccess)
                return commandMatch;
            
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
            if (bestMatch == null) return commandMatch;

            command = bestMatch.SimilarTo;
            query = command + " " + args;
            var (commandMatch2, result2) = await CommandService.FindAsync(context, query, null);
            return result2.IsSuccess ? commandMatch : commandMatch2;
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

        private static async Task SendErrorMessage(SocketUserMessage message, ILocalizationProvider loc, string description) {
            (await message.Channel.SendMessageAsync(null, false, BuildErrorEmbed(message, loc, description))).DelayedDelete(Constants.LongTimeSpan);
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

        public CommandInfo GetCommandByName(string commandName) {
            return CommandAliases[commandName].First();
        }
    }
}