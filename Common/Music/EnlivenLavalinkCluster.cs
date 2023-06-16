using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Lavalink4NET.Events;
using Lavalink4NET.Logging;

namespace Common.Music
{
    public class EnlivenLavalinkCluster : LavalinkCluster
    {
        public EnlivenLavalinkCluster(LavalinkClusterOptions options, IDiscordClientWrapper client, ILogger? logger = null, ILavalinkCache? cache = null) 
            : base(options, client, logger, cache)
        {
            NodeConnected += OnNodeConnected;
        }
        
        public bool IsAnyNodeAvailable => Nodes.Any(node => node.IsConnected);

        private TaskCompletionSource<Unit>? _nodeAvailableCompletionSource;
        public Task NodeAvailableTask => IsAnyNodeAvailable
            ? Task.CompletedTask
            : (_nodeAvailableCompletionSource ??= new TaskCompletionSource<Unit>()).Task;
        
        private Task OnNodeConnected(object sender, NodeConnectedEventArgs args) {
            _nodeAvailableCompletionSource?.SetResult(Unit.Default);
            _nodeAvailableCompletionSource = null;
            return Task.CompletedTask;
        }
    }
}