using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Common;
using Discord;
using Discord.WebSocket;
using NLog;

namespace Bot.DiscordRelated.MessageComponents {
    public class MessageComponentService {
        private EnlivenShardedClient _enlivenShardedClient;
        private ILogger _logger;
        public MessageComponentService(EnlivenShardedClient enlivenShardedClient, ILogger logger) {
            _logger = logger;
            _enlivenShardedClient = enlivenShardedClient;
        }

        public IObservable<SocketMessageComponent> MessageComponentUse => _enlivenShardedClient.MessageComponentUse;

        public EnlivenComponentManager GetBuilder() {
            return new EnlivenComponentManager(this);
        }

        public IDisposable RegisterMessageComponent(string id, Action<SocketMessageComponent> onComponentUse) {
            var registrationStacktrace = Environment.StackTrace;
            return _enlivenShardedClient.MessageComponentUse.Where(component => component.Data.CustomId == id).Subscribe(component => {
                try {
                    onComponentUse(component);
                } catch (Exception e) {
                    _logger.Error(e, "Exception in handling message component callback. RegisterMessageComponent stacktrace:\n {RegistrationStacktrace}", registrationStacktrace);
                }
            });
        }
    }
}