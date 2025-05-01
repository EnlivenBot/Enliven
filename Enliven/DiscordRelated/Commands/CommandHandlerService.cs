using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Config.Emoji;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Utils;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.Commands;

public class CommandHandlerService : IService
{
    private readonly DiscordShardedClient _client;
    private readonly CollectorService _collectorService;
    private readonly CommandCooldownHandler _commandCooldownHandler = new();
    private readonly CustomCommandService _commandService;
    private readonly FuzzySearch _fuzzySearch = new();
    private readonly IGuildConfigProvider _guildConfigProvider;
    private readonly ILifetimeScope _serviceProvider;
    private readonly IStatisticsPartProvider _statisticsPartProvider;
    private readonly ILogger<CommandHandlerService> _logger;

    public CommandHandlerService(DiscordShardedClient client, CustomCommandService commandService,
        IGuildConfigProvider guildConfigProvider,
        IStatisticsPartProvider statisticsPartProvider, ILifetimeScope serviceProvider,
        CollectorService collectorService,
        ILogger<CommandHandlerService> logger)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _collectorService = collectorService;
        _statisticsPartProvider = statisticsPartProvider;
        _guildConfigProvider = guildConfigProvider;
        _client = client;
        _commandService = commandService;
    }


    public Task OnPostDiscordStart()
    {
        _fuzzySearch.AddData(_commandService.Aliases.Select(infos => infos.Key));

        _client.MessageReceived += HandleCommand;
        return Task.CompletedTask;
    }

    private async Task HandleCommand(SocketMessage s)
    {
        if (s is not SocketUserMessage { Source: MessageSource.User } msg) return;
        if (s.Channel is not SocketGuildChannel guildChannel) return;

        var context = new CommandContext(_client, msg);
        var argPos = 0;
        var guildConfig = _guildConfigProvider.Get(guildChannel.Guild.Id);

        var hasStringPrefix = msg.HasStringPrefix(guildConfig.Prefix, ref argPos);
        var hasMentionPrefix = HasMentionPrefix(msg, _client.CurrentUser, ref argPos);
        var isDedicatedMusicChannel = IsDedicatedMusicChannel(msg, guildConfig);

        var isCommand = false;
        if (hasStringPrefix || hasMentionPrefix || isDedicatedMusicChannel)
        {
            isCommand = true;
            var query = msg.Content.Try(s1 => s1.Substring(argPos), "");
            if (string.IsNullOrEmpty(query)) query = " ";
            if (string.IsNullOrWhiteSpace(query) && hasMentionPrefix) query = "help";
            if (string.IsNullOrWhiteSpace(query) && isDedicatedMusicChannel) return;

            var command = await GetCommand(query, context);
            if (command.Item2.Error == CommandError.UnknownCommand && isDedicatedMusicChannel)
            {
                query = $"play {query}";
                command = await GetCommand(query, context);
            }

            if (command.Item1 == null)
            {
                if (command.Item2.Error == CommandError.UnmetPrecondition)
                    await SendErrorMessage(msg, guildConfig.Loc,
                        guildConfig.Loc.Get("CommandHandler.UnmetPrecondition"));
                else
                {
                    var bestMatches = _fuzzySearch.Search(query).GetBestMatches(3)
                        .Select(match => $"`{match.SimilarTo}`").JoinToString(", ");
                    var entryLocalized =
                        new EntryLocalized("CommandHandler.UnknownCommand").Add(query.SafeSubstring(40, "...")!,
                            bestMatches);
                    await AddEmojiErrorHint(msg, guildConfig.Loc, CommonEmoji.Help, entryLocalized);
                }

                return;
            }

            var result = command.Item1.Value.Value.IsSuccess
                ? await ExecuteCommand(msg, query, context, command.Item1.Value, s.Author.Id.ToString())
                : command.Item1.Value.Value;

            if (!result.IsSuccess)
            {
                switch (result.Error)
                {
                    case CommandError.ParseFailed:
                        await AddEmojiErrorHint(msg, guildConfig.Loc, CommonEmoji.Help,
                            new EntryLocalized("CommandHandler.ParseFailed"),
                            _commandService.BuildHelpFields(command.Item1.Value.Key.Alias, guildConfig.Loc));
                        break;
                    case CommandError.BadArgCount:
                        await AddEmojiErrorHint(msg, guildConfig.Loc, CommonEmoji.Help,
                            new EntryLocalized("CommandHandler.BadArgCount"),
                            _commandService.BuildHelpFields(command.Item1.Value.Key.Alias, guildConfig.Loc));
                        break;
                    case CommandError.ObjectNotFound:
                    case CommandError.MultipleMatches:
                        await SendErrorMessage(msg, guildConfig.Loc, result.ErrorReason);
                        break;
                }

                if (result.Error == CommandError.Exception)
                {
                    var exception = result is ExecuteResult executeResult ? executeResult.Exception : null;
                    if (exception is not CommandInterruptionException)
                        _logger.LogError(exception, "Interaction execution {Result}: {Reason}", result.Error!.Value,
                            result.ErrorReason);
                }
            }
        }
    }

    private static bool IsDedicatedMusicChannel(IMessage msg, GuildConfig guildConfig)
    {
        return guildConfig.GetChannel(ChannelFunction.DedicatedMusic, out var channelId) && msg.Channel.Id == channelId;
    }

    private async Task AddEmojiErrorHint(SocketUserMessage targetMessage, ILocalizationProvider loc, IEmote emote,
        IEntry description,
        IEnumerable<EmbedFieldBuilder>? builders = null)
    {
        var collector = _collectorService.CollectReaction(targetMessage,
            reaction => reaction.UserId == targetMessage.Author.Id,
            async eventArgs =>
            {
                eventArgs.Controller.Dispose();
                _ = eventArgs.RemoveReason();
                _ = targetMessage.RemoveReactionAsync(CommonEmoji.Help, _client.CurrentUser);

                await SendErrorMessage(targetMessage, loc, description.Get(loc), builders);
            });
        try
        {
            var addReactionAsync = targetMessage.AddReactionAsync(emote);
            _ = addReactionAsync.ContinueWith(async _ =>
            {
                await Task.Delay(TimeSpan.FromSeconds(20));
                try
                {
                    await targetMessage.RemoveReactionAsync(CommonEmoji.Help, _client.CurrentUser);
                }
                finally
                {
                    collector.Dispose();
                }
            });
            await addReactionAsync;
        }
        catch (Exception)
        {
            collector.Dispose();
        }
    }

    private async Task<(KeyValuePair<CommandMatch, ParseResult>?, IResult)> GetCommand(string query,
        ICommandContext context)
    {
        var (commandMatch, result) = await _commandService.FindAsync(context, query, null);
        if (result.IsSuccess) return (commandMatch, result);

        if (result.Error != CommandError.UnknownCommand) return (commandMatch, result);
        string args;
        var command = query.Substring(0,
            query.IndexOf(" ", StringComparison.Ordinal) > 0
                ? query.IndexOf(" ", StringComparison.Ordinal)
                : query.Length);
        try
        {
            args = query.Substring(command.Length + 1);
        }
        catch
        {
            args = "";
        }

        var searchResult = _fuzzySearch.Search(command);
        var bestMatch = searchResult.GetFullMatch();

        // Check for a another keyboard layout
        if (bestMatch == null) return (commandMatch, result);

        command = bestMatch.SimilarTo;
        query = $"{command} {args}";
        var (commandMatch2, result2) = await _commandService.FindAsync(context, query, null);
        return result2.IsSuccess ? (commandMatch2, result2) : (commandMatch, result);
    }

    public async Task<IResult> ExecuteCommand(IMessage message, string query, ICommandContext context,
        KeyValuePair<CommandMatch, ParseResult> pair,
        string authorId)
    {
        if (_commandCooldownHandler.IsCommandOnCooldown(pair.Key.Command, context))
        {
            var localizationProvider = _guildConfigProvider.Get(context.Guild.Id).Loc;
            await SendErrorMessage(message, localizationProvider,
                new EntryLocalized("CommandHandler.CommandOnCooldown").Get(localizationProvider));
            return ExecuteResult.FromSuccess();
        }

#pragma warning disable 618
        var result = await pair.Key.ExecuteAsync(context, pair.Value, new ServiceProviderAdapter(_serviceProvider));

        if (result.Error != CommandError.UnknownCommand)
        {
            var commandName = query.IndexOf(" ", StringComparison.Ordinal) > -1
                ? query.Substring(0, query.IndexOf(" ", StringComparison.Ordinal))
                : query;
            _statisticsPartProvider.RegisterUsage(commandName, authorId);
            _statisticsPartProvider.RegisterUsage(commandName, "Global");
        }

        return result;
#pragma warning restore 618
    }

    public async Task<IResult> ExecuteCommand(string query, ICommandContext context, string authorId)
    {
        var result = await _commandService.ExecuteAsync(context, query, new ServiceProviderAdapter(_serviceProvider));
        if (result.Error != CommandError.UnknownCommand)
        {
            var commandName = query.IndexOf(" ", StringComparison.Ordinal) > -1
                ? query.Substring(0, query.IndexOf(" ", StringComparison.Ordinal))
                : query;
            _statisticsPartProvider.RegisterUsage(commandName, authorId);
            _statisticsPartProvider.RegisterUsage(commandName, "Global");
        }

        return result;
    }

    private static async Task SendErrorMessage(IMessage message, ILocalizationProvider loc, string description,
        IEnumerable<EmbedFieldBuilder>? fieldBuilders)
    {
        if (fieldBuilders == null)
        {
            await SendErrorMessage(message, loc, description);
            return;
        }

        var embed = GetErrorEmbed(message.Author, loc, description).WithFields(fieldBuilders).Build();
        await message.Channel.SendMessageAsync(null, false, embed).DelayedDelete(Constants.LongTimeSpan);
    }

    private static async Task SendErrorMessage(IMessage message, ILocalizationProvider loc, string description)
    {
        var embed = GetErrorEmbed(message.Author, loc, description).Build();
        await message.Channel.SendMessageAsync(null, false, embed).DelayedDelete(Constants.LongTimeSpan);
    }

    public static EmbedBuilder GetErrorEmbed(IUser user, ILocalizationProvider loc, string description)
    {
        var builder = new EmbedBuilder();
        builder.WithFooter(loc.Get("Commands.RequestedBy").Format(user.Username), user.GetAvatarUrl())
            .WithColor(Color.Orange);
        builder.WithTitle(loc.Get("CommandHandler.FailedTitle"))
            .WithDescription(description);
        return builder;
    }

    private static bool HasMentionPrefix(IUserMessage msg, IUser user, ref int argPos)
    {
        var content = msg.Content;
        if (string.IsNullOrEmpty(content) || content.Length <= 3 || content[0] != '<' || content[1] != '@')
            return false;
        var num = content.IndexOf('>');
        if (num == -1 || !MentionUtils.TryParseUser(content.Substring(0, num + 1), out var userId) ||
            (long)userId != (long)user.Id)
            return false;
        argPos = num + 2;
        return true;
    }

    public CommandInfo GetCommandByName(string commandName)
    {
        return _commandService.Aliases[commandName].First();
    }
}