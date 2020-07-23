using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated {
    // This service requires that your bot is being run by a daemon that handles
    // Exit Code 1 (or any exit code) as a restart.
    //
    // If you do not have your bot setup to run in a daemon, this service will just
    // terminate the process and the bot will not restart.
    // 
    // Links to daemons:
    // [Powershell (Windows+Unix)] https://gitlab.com/snippets/21444
    // [Bash (Unix)] https://stackoverflow.com/a/697064
    public class ReliabilityService {
        // --- Begin Configuration Section ---
        // How long should we wait on the client to reconnect before resetting?
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        // Should we attempt to reset the client? Set this to false if your client is still locking up.
        private static readonly bool _attemptReset = true;

        // Change log levels if desired:
        private static readonly LogSeverity _debug = LogSeverity.Debug;
        private static readonly LogSeverity _info = LogSeverity.Info;

        private static readonly LogSeverity _critical = LogSeverity.Critical;
        // --- End Configuration Section ---

        private readonly Func<LogMessage, Task> _logger;
        private static Dictionary<IDiscordClient, CancellationTokenSource> _disconnectedClients = new Dictionary<IDiscordClient, CancellationTokenSource>();

        public ReliabilityService(DiscordSocketClient mainDiscord, Func<LogMessage, Task>? logger = null) {
            _logger = logger ?? (_ => Task.CompletedTask);

            mainDiscord.Connected += () => ConnectedAsync(mainDiscord);
            mainDiscord.Disconnected += exception => DisconnectedAsync(exception, mainDiscord);
        }

        public ReliabilityService(DiscordShardedClient mainDiscord, Func<LogMessage, Task> logger) {
            _logger = logger ?? (_ => Task.CompletedTask);

            mainDiscord.ShardConnected += ConnectedAsync;
            mainDiscord.ShardDisconnected += DisconnectedAsync;
        }

        private Task DisconnectedAsync(Exception arg1, DiscordSocketClient arg2) {
            // Check the state after <timeout> to see if we reconnected
            _ = InfoAsync("Client disconnected, starting timeout task...");
            _disconnectedClients[arg2] = new CancellationTokenSource();
            _ = Task.Delay(Timeout, _disconnectedClients[arg2].Token).ContinueWith(async _ => {
                await DebugAsync("Timeout expired, continuing to check client state...");
                await CheckStateAsync(arg2);
                await DebugAsync("State came back okay");
            });

            return Task.CompletedTask;
        }

        private Task ConnectedAsync(DiscordSocketClient arg) {
            // Cancel all previous state checks and reset the CancelToken - client is back online
            _ = DebugAsync("Client reconnected, resetting cancel tokens...");
            if (_disconnectedClients.TryGetValue(arg, out var cts)) {
                cts.Cancel();
            }
            
            _ = DebugAsync("Client reconnected, cancel tokens reset.");

            return Task.CompletedTask;
        }
        
        private async Task CheckStateAsync(IDiscordClient client) {
            // Client reconnected, no need to reset
            if (client.ConnectionState == ConnectionState.Connected) return;
            if (_attemptReset) {
                await InfoAsync("Attempting to reset the client");

                var timeout = Task.Delay(Timeout);
                var connect = client.StartAsync();
                var task = await Task.WhenAny(timeout, connect);

                if (task == timeout) {
                    await CriticalAsync("Client reset timed out (task deadlocked?), killing process");
                    FailFast();
                }
                else if (connect.IsFaulted) {
                    await CriticalAsync("Client reset faulted, killing process", connect.Exception);
                    FailFast();
                }
                else if (connect.IsCompletedSuccessfully)
                    await InfoAsync("Client reset successfully!");

                return;
            }

            await CriticalAsync("Client did not reconnect in time, killing process");
            FailFast();
        }

        private void FailFast()
            => Environment.Exit(1);

        // Logging Helpers
        private const string LogSource = "Reliability";

        private Task DebugAsync(string message)
            => _logger.Invoke(new LogMessage(_debug, LogSource, message));

        private Task InfoAsync(string message)
            => _logger.Invoke(new LogMessage(_info, LogSource, message));

        private Task CriticalAsync(string message, Exception? error = null)
            => _logger.Invoke(new LogMessage(_critical, LogSource, message, error));
    }
}