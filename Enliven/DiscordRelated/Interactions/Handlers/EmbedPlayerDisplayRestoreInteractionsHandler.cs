using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Common;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using ExecuteResult = Discord.Interactions.ExecuteResult;
using IResult = Discord.Interactions.IResult;

namespace Bot.DiscordRelated.Interactions.Handlers;

public class EmbedPlayerDisplayRestoreInteractionsHandler(
    EnlivenShardedClient client,
    CommandHandlerService commandHandlerService) : IInteractionsHandler {
    public async ValueTask<IResult?> Handle(IInteractionContext context) {
        if (context.Interaction is not IComponentInteraction socketInteraction)
            return null;

        if (socketInteraction.Data.CustomId != "restoreStoppedPlayer")
            return null;

        var guild = (context.Channel as IGuildChannel)?.Guild;
        if (guild == null)
            return null;
        var commandContext = new ControllableCommandContext(client)
            { Guild = guild, Channel = context.Channel, User = socketInteraction.User };
        var result = await commandHandlerService.ExecuteCommand("resume", commandContext, context.User.Id.ToString());

        if (result.IsSuccess)
            return ExecuteResult.FromSuccess();

        var error = result.Error switch {
            CommandError.UnknownCommand => InteractionCommandError.UnknownCommand,
            CommandError.ParseFailed => InteractionCommandError.ParseFailed,
            CommandError.BadArgCount => InteractionCommandError.BadArgs,
            CommandError.ObjectNotFound => InteractionCommandError.Unsuccessful,
            CommandError.MultipleMatches => InteractionCommandError.Unsuccessful,
            CommandError.UnmetPrecondition => InteractionCommandError.UnmetPrecondition,
            CommandError.Exception => InteractionCommandError.Exception,
            CommandError.Unsuccessful => InteractionCommandError.Unsuccessful,
            null => InteractionCommandError.Unsuccessful,
            _ => throw new ArgumentOutOfRangeException()
        };

        return ExecuteResult.FromError(error, result.ErrorReason);
    }
}