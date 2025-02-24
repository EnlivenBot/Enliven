﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Bot.Utilities.Logging;
using Common;
using Common.Config;
using Common.Utils;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;

namespace Bot;

public class EnlivenBotWrapper
{
    private static ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IConfiguration _configuration;
    private readonly InstanceConfig _instanceConfig;
    private TaskCompletionSource<bool>? _firstStartResult;

    public EnlivenBotWrapper(InstanceConfig config, IConfiguration configuration)
    {
        _instanceConfig = config;
        _configuration = configuration;
    }

    /// <summary>
    /// Attempts to start bot instance
    /// </summary>
    /// <returns>True if start successful, otherwise False</returns>
    public Task<bool> StartAsync(ILifetimeScope container, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (_firstStartResult != null) throw new Exception("Current instance already started");

        _firstStartResult = new TaskCompletionSource<bool>();

        _ = RunLoopAsync(container, serviceProvider, cancellationToken);

        return _firstStartResult!.Task;
    }

    private async Task RunLoopAsync(ILifetimeScope container, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var isFirst = true;
        var topLevelHostedServices = serviceProvider.GetServices<IHostedService>()
            .ToImmutableArray();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var lifetimeScope =
                    container.BeginLifetimeScope(Constants.BotLifetimeScopeTag, ConfigureBotLifetime);
                var bot = lifetimeScope.Resolve<EnlivenBot>();
                await bot.StartAsync(cancellationToken);
                var hostedServices = lifetimeScope
                    .Resolve<IReadOnlyCollection<IHostedService>>()
                    .Except(topLevelHostedServices)
                    .ToImmutableArray();
                foreach (var hostedService in hostedServices) await hostedService.StartAsync(cancellationToken);

                _firstStartResult!.TrySetResult(true);

                try
                {
                    await bot.WaitForDisposeAsync();
                    foreach (var hostedService in hostedServices) await hostedService.StopAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            catch (Exception e)
            {
                _logger.Fatal(e, $"Failed to start bot instance with config {Path.GetFileName(_instanceConfig.Name)}");
                _firstStartResult!.TrySetResult(false);
                if (isFirst) return;
            }

            isFirst = false;
        }
    }

    private void ConfigureBotLifetime(ContainerBuilder builder)
    {
        builder.RegisterModule<BotInstanceNlogModule>();

        var services = new ServiceCollection();
        services.AddSingleton(_instanceConfig);
        services.AddSingleton<IServiceScopeFactory, ServiceScopeFactoryAdapter>();
        services.AddSingleton<EnlivenBot>();
        services.ConfigureNamedOptions<DiscordSocketConfig>(_configuration);
        services.AddSingleton<EnlivenShardedClient>();
        services.AddSingleton<DiscordShardedClient>(s => s.GetRequiredService<EnlivenShardedClient>());
        services.AddSingleton<IDiscordClient>(s => s.GetRequiredService<EnlivenShardedClient>());
        services.AddLavalink();
        services.AddPerBotServices();

        builder.Populate(services);
    }
}