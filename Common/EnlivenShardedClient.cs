using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Common {
    public class EnlivenShardedClient : DiscordShardedClient {
        private readonly TaskCompletionSource<object> _readyTaskCompletionSource = new TaskCompletionSource<object>();

        public EnlivenShardedClient() : base() {
            SubscribeToReady();
        }

        public EnlivenShardedClient(DiscordSocketConfig config) : base(config) {
            SubscribeToReady();
            SubscribeToMessageComponents();
        }

        public EnlivenShardedClient(int[] ids) : base(ids) {
            SubscribeToReady();
            SubscribeToMessageComponents();
        }

        public EnlivenShardedClient(int[] ids, DiscordSocketConfig config) : base(ids, config) {
            SubscribeToReady();
            SubscribeToMessageComponents();
        }
        public bool IsReady => Ready.IsCompleted;
        public Task Ready => _readyTaskCompletionSource.Task;
        public IObservable<SocketMessageComponent> MessageComponentUse { get; private set; } = null!;

        private void SubscribeToReady() {
            ShardReady += client => {
                _readyTaskCompletionSource.TrySetResult(true);
                return Task.CompletedTask;
            };
        }

        private void SubscribeToMessageComponents() {
            MessageComponentUse = Observable.FromEvent<Func<SocketInteraction, Task>, SocketInteraction>(
                    action => InteractionCreated += action,
                    action => InteractionCreated -= action)
                .Where(interaction => interaction.Type == InteractionType.MessageComponent)
                .Select(interaction => (SocketMessageComponent)interaction);
        }
    }
}