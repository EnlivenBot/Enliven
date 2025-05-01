using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Bot.Utilities.Logging;
using Common;
using Common.Config;
using Common.Utils;
using Discord;
using Discord.Net;
using Microsoft.Extensions.Logging;

namespace Bot;

public class EnlivenBot : AsyncDisposableBase, IService
{
    private readonly EnlivenShardedClient _client;
    private readonly InstanceConfig _config;
    private readonly ILogger<EnlivenBot> _logger;
    private readonly IEnumerable<IService> _services;
    private bool _isDiscordStarted;

    public EnlivenBot(ILogger<EnlivenBot> logger, IEnumerable<IService> services,
        EnlivenShardedClient discordShardedClient, InstanceConfig config)
    {
        _config = config;

        _services = services;
        _logger = logger;
        _client = discordShardedClient;
    }

    public async Task OnShutdown(bool isDiscordStarted)
    {
        if (isDiscordStarted)
        {
            await _client.SetStatusAsync(UserStatus.AFK);
            await _client.SetGameAsync("Reboot...");
        }
    }

    public async Task OnPostDiscordStart()
    {
        await _client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
    }

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.Register(Dispose);

        _logger.LogInformation("Start Initialising");
        await IService.ProcessEventAsync(_services, ServiceEventType.PreDiscordLogin, _logger);
        _client.Log += message => LoggingUtilities.OnDiscordLog(_logger, message);

        await Task.Run(async () => await LoginAsync(cancellationToken).ObserveException(), cancellationToken);
        await IService.ProcessEventAsync(_services, ServiceEventType.PreDiscordStart, _logger);

        _logger.LogInformation("Starting client");
        await _client.StartAsync();
        _isDiscordStarted = true;
        await IService.ProcessEventAsync(_services, ServiceEventType.PostDiscordStart, _logger);

        _ = _client.Ready.ContinueWith(
            async _ => await IService.ProcessEventAsync(_services, ServiceEventType.DiscordReady, _logger),
            cancellationToken);
    }

    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start logining");
        for (var connectionTryNumber = 1; connectionTryNumber <= 5; connectionTryNumber++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Run(() => _client.LoginAsync(TokenType.Bot, _config.BotToken), cancellationToken);
                _logger.LogInformation("Successefully logged in");
                return;
            }
            catch (HttpException e) when (e.HttpCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogError("Failed to login - unauthorized. Check token - {Token}", _config.BotToken);
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to login");
                _logger.LogInformation("Waiting before next attempt - {Delay}s", connectionTryNumber * 10);
                await Task.Delay(TimeSpan.FromSeconds(connectionTryNumber * 10), cancellationToken);
            }
        }

        _logger.LogError("Failed to login 5 times. Quiting");
        throw new Exception("Failed to login 5 times");
    }

    protected override async Task DisposeInternalAsync()
    {
        await IService.ProcessEventAsync(_services,
            _isDiscordStarted ? ServiceEventType.ShutdownStarted : ServiceEventType.ShutdownNotStarted, _logger);
        await _client.DisposeAsync();
    }
}