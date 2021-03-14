using System.Threading.Tasks;
using Discord.WebSocket;

namespace Common {
    public class EnlivenShardedClient : DiscordShardedClient {
        public bool IsReady => Ready.IsCompleted;
        public Task Ready => _readyTaskCompletionSource.Task;
        private TaskCompletionSource<object> _readyTaskCompletionSource = new TaskCompletionSource<object>();

        public EnlivenShardedClient() {
            SubscribeToReady();
        }

        public EnlivenShardedClient(DiscordSocketConfig config) : base(config) {
            SubscribeToReady();
        }

        public EnlivenShardedClient(int[] ids) : base(ids) {
            SubscribeToReady();
        }

        public EnlivenShardedClient(int[] ids, DiscordSocketConfig config) : base(ids, config) {
            SubscribeToReady();
        }

        private void SubscribeToReady() {
            ShardReady += client => {
                _readyTaskCompletionSource.TrySetResult(true);
                return Task.CompletedTask;
            };
        }
    }
}