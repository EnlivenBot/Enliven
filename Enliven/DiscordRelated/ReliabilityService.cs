﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Common;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bot.DiscordRelated;

// This service requires that your bot is being run by a daemon that handles
// Exit Code 1 (or any exit code) as a restart.
//
// If you do not have your bot setup to run in a daemon, this service will just
// terminate the process and the bot will not restart.
// 
// Links to daemons:
// [Powershell (Windows+Unix)] https://gitlab.com/snippets/21444
// [Bash (Unix)] https://stackoverflow.com/a/697064
public class ReliabilityService : IService
{
    // --- Begin Configuration Section ---
    // How long should we wait on the client to reconnect before resetting?
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Should we attempt to reset the client? Set this to false if your client is still locking up.
    private static readonly bool AttemptReset = true;
    // --- End Configuration Section ---

    private readonly Dictionary<IDiscordClient, CancellationTokenSource> _disconnectedClients = new();
    private readonly ILogger<ReliabilityService> _logger;

    public ReliabilityService(DiscordSocketClient mainDiscord, ILogger<ReliabilityService> logger)
    {
        _logger = logger;

        mainDiscord.Connected += () => ConnectedAsync(mainDiscord);
        mainDiscord.Disconnected += exception => DisconnectedAsync(exception, mainDiscord);
    }

    public ReliabilityService(DiscordShardedClient mainDiscord, ILogger<ReliabilityService> logger)
    {
        _logger = logger;

        mainDiscord.ShardConnected += ConnectedAsync;
        mainDiscord.ShardDisconnected += DisconnectedAsync;
    }

    public Task OnPostDiscordStart()
    {
        return Task.CompletedTask;
    }

    private Task DisconnectedAsync(Exception arg1, DiscordSocketClient arg2)
    {
        // Check the state after <timeout> to see if we reconnected
        _logger.LogInformation("Client disconnected, starting timeout task...");
        _disconnectedClients[arg2] = new CancellationTokenSource();
        _ = Task.Delay(Timeout, _disconnectedClients[arg2].Token).ContinueWith(async _ =>
        {
            _logger.LogDebug("Timeout expired, continuing to check client state...");
            if (await CheckIsStateCorrectAsync(arg2))
                _logger.LogDebug("State came back okay");
            else
                FailFast();
        });

        return Task.CompletedTask;
    }

    private Task ConnectedAsync(DiscordSocketClient arg)
    {
        // Cancel all previous state checks and reset the CancelToken - client is back online
        _logger.LogDebug("Client reconnected, resetting cancel tokens...");
        if (_disconnectedClients.TryGetValue(arg, out var cts)) cts!.Cancel();

        _logger.LogDebug("Client reconnected, cancel tokens reset.");

        return Task.CompletedTask;
    }

    private async Task<bool> CheckIsStateCorrectAsync(IDiscordClient client)
    {
        // Client reconnected, no need to reset
        if (client.ConnectionState == ConnectionState.Connected) return true;
        if (AttemptReset)
        {
            _logger.LogInformation("Attempting to reset the client");

            var timeout = Task.Delay(Timeout);
            var connect = client.StartAsync();
            var task = await Task.WhenAny(timeout, connect);

            if (task == timeout)
            {
                _logger.LogCritical(null, "Client reset timed out (task deadlocked?), killing process");
                return false;
            }

            if (connect.IsFaulted)
            {
                _logger.LogCritical(connect.Exception, "Client reset faulted, killing process");
                return false;
            }

            if (connect.IsCompletedSuccessfully)
                _logger.LogInformation("Client reset successfully!");

            return true;
        }

        _logger.LogCritical(null, "Client did not reconnect in time, killing process");
        return false;
    }

    protected virtual void FailFast()
    {
        Environment.Exit(1);
    }
}

public class ScopedReliabilityService : ReliabilityService
{
    private readonly ILifetimeScope _lifetimeScope;

    public ScopedReliabilityService(DiscordSocketClient mainDiscord, ILogger<ScopedReliabilityService> logger, ILifetimeScope lifetimeScope) :
        base(mainDiscord, logger)
    {
        _lifetimeScope = lifetimeScope;
    }

    public ScopedReliabilityService(DiscordShardedClient mainDiscord, ILogger<ScopedReliabilityService> logger, ILifetimeScope lifetimeScope) :
        base(mainDiscord, logger)
    {
        _lifetimeScope = lifetimeScope;
    }

    protected override void FailFast()
    {
        _lifetimeScope.Dispose();
    }
}