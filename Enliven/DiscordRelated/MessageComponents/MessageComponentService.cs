using System;
using System.Reactive.Linq;
using Common;
using Discord.WebSocket;
using NLog;

namespace Bot.DiscordRelated.MessageComponents {
    public class MessageComponentService {
        private ILogger _logger;
        public MessageComponentService(EnlivenShardedClient enlivenShardedClient, ILogger logger) {
            _logger = logger;
            MessageComponentUse = enlivenShardedClient.MessageComponentUse
                .Do(component => _ = component.DeferAsync());
        }

        public IObservable<SocketMessageComponent> MessageComponentUse { get; }

        public EnlivenComponentBuilder GetBuilder() {
            return new EnlivenComponentBuilder(this);
        }

        public IDisposable RegisterMessageComponent(string id, Action<SocketMessageComponent> onComponentUse) {
            var registrationStacktrace = Environment.StackTrace;
            return MessageComponentUse
                .Where(component => component.Data.CustomId == id)
                .Subscribe(component => {
                    try {
                        onComponentUse(component);
                    }
                    catch (Exception e) {
                        _logger.Error(e, "Exception in handling message component callback. RegisterMessageComponent stacktrace:\n {RegistrationStacktrace}", registrationStacktrace);
                    }
                });
        }
    }
}