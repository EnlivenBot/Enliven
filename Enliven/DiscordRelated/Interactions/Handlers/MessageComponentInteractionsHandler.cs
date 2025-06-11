using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.MessageComponents;
using Common;
using Common.Utils;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bot.DiscordRelated.Interactions.Handlers;

public class MessageComponentInteractionsHandler : IInteractionsHandler {
    private readonly ConcurrentDictionary<string, List<MessageComponentInteractionsRegistration>> _messageComponentHandlers = new();
    private readonly ILogger<MessageComponentInteractionsHandler> _logger;
    public MessageComponentInteractionsHandler(ILogger<MessageComponentInteractionsHandler> logger) {
        _logger = logger;
    }
    
    public EnlivenComponentBuilder GetBuilder() {
        return new EnlivenComponentBuilder(this);
    }

    public IDisposable RegisterMessageComponent(string id, Func<SocketMessageComponent, ValueTask> onComponentUse)
    {
        var registration = _messageComponentHandlers.GetOrAdd(id, _ => []);
        var componentInteractionsRegistration = new MessageComponentInteractionsRegistration(onComponentUse);
        registration.Add(componentInteractionsRegistration);
        
        return new DisposableAction(() =>
        {
            registration.Remove(componentInteractionsRegistration);
            if (!registration.Any())
            {
                _messageComponentHandlers.Remove(id, out _);
            }
        });
    }

    public async ValueTask<IResult?> Handle(ShardedInteractionContext context)
    {
        if (context.Interaction is not SocketMessageComponent messageComponent)
        {
            return null;
        }
        
        if (!_messageComponentHandlers.TryGetValue(messageComponent.Data.CustomId, out var handlersList))
        {
            return null;
        }

        try
        {
            await handlersList
                .Select(registration => registration.HandleAsync(messageComponent, _logger))
                .WhenAll();

            return ExecuteResult.FromSuccess();
        }
        catch (Exception e)
        {
            return ExecuteResult.FromError(e);
        }
    }
    
    private class MessageComponentInteractionsRegistration(Func<SocketMessageComponent, ValueTask> action)
    {
        private readonly string _stackTrace = new StackTrace(1).ToString();

        public async Task HandleAsync(SocketMessageComponent interaction, ILogger logger)
        {
            try {
                await action(interaction);
            }
            catch (Exception e) {
                logger.LogError(e, "Exception in handling message component callback. RegisterMessageComponent stacktrace:\n {RegistrationStacktrace}", _stackTrace);
                throw;
            }
        }
    }
}