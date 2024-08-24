using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Lavalink4NET.Cluster;
using Lavalink4NET.Cluster.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common.Config;

public class ClusterAudioServiceOptionsConfigurator : IConfigureOptions<ClusterAudioServiceOptions>
{
    private readonly ILogger<ClusterAudioServiceOptionsConfigurator> _logger;
    private readonly IEnumerable<LavalinkNodeInfo> _nodes;
    private int _nodeIdCounter;

    public ClusterAudioServiceOptionsConfigurator(IConfiguration configuration, InstanceConfig instanceConfig,
        ILogger<ClusterAudioServiceOptionsConfigurator> logger)
    {
        _logger = logger;
        var commonNodes = configuration.GetSectionValue<IEnumerable<LavalinkNodeInfo>>("LavalinkNodes") ??
                          Array.Empty<LavalinkNodeInfo>();
        _nodes = commonNodes.Concat(instanceConfig.LavalinkNodes).Distinct();
    }

    /// <inheritdoc />
    public void Configure(ClusterAudioServiceOptions options)
    {
        options.Nodes = _nodes.Select(ConvertNodeInfoToOptions).ToImmutableArray();
    }

    private LavalinkClusterNodeOptions ConvertNodeInfoToOptions(LavalinkNodeInfo info)
    {
        var label = info.Name;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = $"Node №{++_nodeIdCounter}";
            _logger.LogInformation("Name {NodeName} assigned to node with url {NodeUrl}", label, info.RestUri);
        }

        return new LavalinkClusterNodeOptions
        {
            BaseAddress = info.RestUri,
            Passphrase = info.Password,
            Label = label
        };
    }
}