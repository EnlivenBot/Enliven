using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Bot.DiscordRelated.Logging;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Config.Emoji;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.Commands {
    public class CommandHandlerService : IService {
        private DiscordShardedClient _client;
        private IGuildConfigProvider _guildConfigProvider;
        private IStatisticsPartProvider _statisticsPartProvider;
        private ILogger _logger;
        private MessageHistoryService _messageHistoryService;
        private ILifetimeScope _serviceProvider;
        private CommandCooldownHandler _commandCooldownHandler = new CommandCooldownHandler();

        public CommandHandlerService(DiscordShardedClient client, CustomCommandService commandService, IGuildConfigProvider guildConfigProvider,
                                     IStatisticsPartProvider statisticsPartProvider, ILogger logger, MessageHistoryService messageHistoryService,
                                     ILifetimeScope serviceProvider) {
            _serviceProvider = serviceProvider;
            _messageHistoryService = messageHistoryService;
            _logger = logger;
            _statisticsPartProvider = statisticsPartProvider;
            _guildConfigProvider = guildConfigProvider;
            _client = client;
            CommandService = commandService;
        }

        public CustomCommandService CommandService { get; private set; }
        public static FuzzySearch FuzzySearch { get; set; } = new FuzzySearch();
        public List<CommandInfo> AllCommands { get; } = new List<CommandInfo>();

        public Task OnPostDiscordStartInitialize() {
            FuzzySearch.AddData(CommandService.Aliases.Select(infos => infos.Key));

            _client.MessageReceived += HandleCommand;
            return Task.CompletedTask;
        }

        private async Task HandleCommand(SocketMessage s) {
            if (!(s is SocketUserMessage msg)
             || msg.Source != MessageSource.User
             || !(s.Channel is SocketGuildChannel guildChannel)) {
                return;
            }

            var context = new CommandContext(_client, msg);
            var argPos = 0;
            var guildConfig = _guildConfigProvider.Get(guildChannel.Guild.Id);

            var hasStringPrefix = msg.HasStringPrefix(guildConfig.Prefix, ref argPos);
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
                        await SendErrorMessage(msg, guildConfig.Loc, guildConfig.Loc.Get("CommandHandler.UnmetPrecondition"));
                    }
                    else {
                        await AddEmojiErrorHint(msg, guildConfig.Loc, CommonEmoji.Help,
                            new EntryLocalized("CommandHandler.UnknownCommand").Add(query.SafeSubstring(40, "...")!,
                                FuzzySearch.Search(query).GetBestMatches(3).Select(match => $"`{match.SimilarTo}`").JoinToString(", "),
                                guildConfig.Prefix));
                    }

                    return;
                }

                var result = command.Item1.Value.Value.IsSuccess
                    ? await ExecuteCommand(msg, query, context, command.Item1.Value, s.Author.Id.ToString())
                    : command.Item1.Value.Value;

                if (!result.IsSuccess) {
                    switch (result.Error) {
                        case CommandError.ParseFailed:
                            await AddEmojiErrorHint(msg, guildConfig.Loc, CommonEmoji.Help, new EntryLocalized("CommandHandler.ParseFailed"),
                                CommandService.BuildHelpFields(command.Item1.Value.Key.Alias, guildConfig.Prefix, guildConfig.Loc));
                            break;
                        case CommandError.BadArgCount:
                            await AddEmojiErrorHint(msg, guildConfig.Loc, CommonEmoji.Help, new EntryLocalized("CommandHandler.BadArgCount"),
                                CommandService.BuildHelpFields(command.Item1.Value.Key.Alias, guildConfig.Prefix, guildConfig.Loc));
                            break;
                        case CommandError.ObjectNotFound:
                            await SendErrorMessage(msg, guildConfig.Loc, result.ErrorReason);
                            break;
                        case CommandError.MultipleMatches:
                            await SendErrorMessage(msg, guildConfig.Loc, result.ErrorReason);
                            break;
                    }
                }
            }

            _messageHistoryService.TryLogCreatedMessage(s, guildConfig, isCommand);
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
                    targetMessage.RemoveReactionAsync(CommonEmoji.Help, EnlivenBot.Client.CurrentUser);
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
            if (_commandCooldownHandler.IsCommandOnCooldown(pair.Key.Command, context)) {
                var localizationProvider = _guildConfigProvider.Get(context.Guild.Id).Loc;
                await SendErrorMessage(message, localizationProvider, new EntryLocalized("CommandHandler.CommandOnCooldown").Get(localizationProvider));
                return ExecuteResult.FromSuccess();
            }
            
            #pragma warning disable 618
            IResult result = CollectorsUtils.OnCommandExecute(pair, context, message)
                ? await pair.Key.ExecuteAsync(context, pair.Value, new ServiceProviderAdapter(_serviceProvider))
                : ExecuteResult.FromSuccess();

            if (result.Error != CommandError.UnknownCommand) {
                var commandName = query.IndexOf(" ", StringComparison.Ordinal) > -1 ? query.Substring(0, query.IndexOf(" ", StringComparison.Ordinal)) : query;
                _statisticsPartProvider.RegisterUsage(commandName, authorId);
                _statisticsPartProvider.RegisterUsage(commandName, "Global");
            }

            return result;
            #pragma warning restore 618
        }

        public async Task<IResult> ExecuteCommand(string query, ICommandContext context, string authorId) {
            var result = await CommandService.ExecuteAsync(context, query, new ServiceProviderAdapter(_serviceProvider));
            if (result.Error != CommandError.UnknownCommand) {
                var commandName = query.IndexOf(" ", StringComparison.Ordinal) > -1 ? query.Substring(0, query.IndexOf(" ", StringComparison.Ordinal)) : query;
                _statisticsPartProvider.RegisterUsage(commandName, authorId);
                _statisticsPartProvider.RegisterUsage(commandName, "Global");
            }

            return result;
        }

        private static async Task SendErrorMessage(IMessage message, ILocalizationProvider loc, string description,
                                                   IEnumerable<EmbedFieldBuilder>? fieldBuilders) {
            if (fieldBuilders == null) {
                await SendErrorMessage(message, loc, description);
                return;
            }

            (await message.Channel.SendMessageAsync(null, false, GetErrorEmbed(message.Author, loc, description).WithFields(fieldBuilders).Build()))
               .DelayedDelete(
                    Constants.LongTimeSpan);
        }

        private static async Task SendErrorMessage(IMessage message, ILocalizationProvider loc, string description) {
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

        public CommandInfo GetCommandByName(string commandName) {
            return CommandService.Aliases[commandName].First();
        }
    }
}