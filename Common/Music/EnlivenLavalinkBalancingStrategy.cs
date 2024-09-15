using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Cluster.LoadBalancing.Strategies;
using Lavalink4NET.Cluster.Nodes;
using Microsoft.Extensions.Options;

namespace Common.Music;

// Basically RoundRobinBalancingStrategy
public class EnlivenLavalinkBalancingStrategy : INodeBalancingStrategy
{
    private readonly RoundRobinBalancingStrategyOptions _options;
    private uint _index;

    public EnlivenLavalinkBalancingStrategy(IOptions<RoundRobinBalancingStrategyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public ValueTask<NodeBalanceResult> ScoreAsync(ImmutableArray<ILavalinkNode> nodes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Starting up all nodes before using
        foreach (var lavalinkNode in nodes)
        {
            if (lavalinkNode.Status != LavalinkNodeStatus.OnDemand) continue;
            var startTask = lavalinkNode.StartAsync(CancellationToken.None);
            if (!startTask.IsCompleted)
            {
                _ = startTask.AsTask();
            }
        }

        var selectedNodeIndex = (Interlocked.Increment(ref _index) - 1) % nodes.Length;
        var nodeScores = new List<ScoredLavalinkNode>(nodes.Length);
        var step = 1.0D / nodes.Length;

        for (var index = 0; index < nodes.Length; index++)
        {
            // distance between selected node and current node
            var distance = index - selectedNodeIndex;

            if (distance < 0)
            {
                distance += nodes.Length;
            }

            var score = step * (nodes.Length - distance);
            var node = nodes[index];
            // Only add available nodes at the moment
            if (node.Status == LavalinkNodeStatus.Available)
            {
                nodeScores.Add(new ScoredLavalinkNode(nodes[index], score));
            }
        }

        var result = new NodeBalanceResult(nodeScores.ToImmutableArray(), _options.Duration);
        return new ValueTask<NodeBalanceResult>(result);
    }
}