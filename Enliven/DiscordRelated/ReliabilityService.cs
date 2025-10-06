using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
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
public class ReliabilityService : IService {
    // --- Begin Configuration Section ---
    // How long should we wait on the client to reconnect before resetting?
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Should we attempt to reset the client? Set this to false if your client is still locking up.
    private static readonly bool AttemptReset = true;
    // --- End Configuration Section ---

    private readonly ConcurrentDictionary<IDiscordClient, CancellationTokenSource> _disconnectedClients = new();
    private readonly ILogger<ReliabilityService> _logger;

    public ReliabilityService(DiscordSocketClient mainDiscord, ILogger<ReliabilityService> logger) {
        _logger = logger;

        mainDiscord.Connected += () => ConnectedAsync(mainDiscord);
        mainDiscord.Disconnected += exception => DisconnectedAsync(exception, mainDiscord);
    }

    public ReliabilityService(DiscordShardedClient mainDiscord, ILogger<ReliabilityService> logger) {
        _logger = logger;

        mainDiscord.ShardConnected += ConnectedAsync;
        mainDiscord.ShardDisconnected += DisconnectedAsync;
    }

    public Task OnPostDiscordStart() {
        return Task.CompletedTask;
    }

    private Task DisconnectedAsync(Exception exception, DiscordSocketClient client) {
        if (_disconnectedClients.ContainsKey(client)) {
            return Task.CompletedTask;
        }

        // Check the state after <timeout> to see if we reconnected
        _logger.LogInformation("Client disconnected, starting reconnect watchdog");
        _disconnectedClients[client] = new CancellationTokenSource();
        _ = Task.Delay(Timeout, _disconnectedClients[client].Token).ContinueWith(async _ => {
            _logger.LogDebug("Watchdog timeout expired, trying to check client state");
            if (await CheckIsStateCorrectAsync(client)) {
                _logger.LogInformation("State came back okay");
                _disconnectedClients.TryRemove(client, out var _);
            }
            else
                FailFast();
        });

        return Task.CompletedTask;
    }

    private Task ConnectedAsync(DiscordSocketClient client) {
        if (!_disconnectedClients.TryGetValue(client, out var cts)) {
            return Task.CompletedTask;
        }

        // Cancel all previous state checks and reset the CancelToken - client is back online
        _logger.LogDebug("Client reconnected, stopping watchdog");
        cts.Cancel();
        _disconnectedClients.TryRemove(client, out _);

        return Task.CompletedTask;
    }

    private async Task<bool> CheckIsStateCorrectAsync(IDiscordClient client) {
        // Client reconnected, no need to reset
        if (client.ConnectionState == ConnectionState.Connected) return true;
        if (AttemptReset) {
            _logger.LogInformation("Attempting to reset the client");

            var timeout = Task.Delay(Timeout);
            var connect = client.StartAsync();
            var task = await Task.WhenAny(timeout, connect);

            if (task == timeout) {
                _logger.LogCritical("Client reset timed out (task deadlocked?), killing process");
                return false;
            }

            if (connect.IsFaulted) {
                _logger.LogCritical(connect.Exception, "Client reset faulted, killing process");
                return false;
            }

            if (connect.IsCompletedSuccessfully)
                _logger.LogInformation("Client reset successfully");

            return true;
        }

        _logger.LogCritical("Client did not reconnect in time, killing process");
        return false;
    }

    protected virtual void FailFast() {
        Environment.Exit(1);
    }
}

public class ScopedReliabilityService : ReliabilityService {
    private readonly Func<EnlivenBot> _lifetimeScope;
    private int _disposed;

    public ScopedReliabilityService(DiscordSocketClient mainDiscord, ILogger<ScopedReliabilityService> logger,
        Func<EnlivenBot> lifetimeScope) :
        base(mainDiscord, logger) {
        _lifetimeScope = lifetimeScope;
    }

    public ScopedReliabilityService(DiscordShardedClient mainDiscord, ILogger<ScopedReliabilityService> logger,
        Func<EnlivenBot> lifetimeScope) :
        base(mainDiscord, logger) {
        _lifetimeScope = lifetimeScope;
    }

    protected override void FailFast() {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) {
            _lifetimeScope().Dispose();
        }
    }
}