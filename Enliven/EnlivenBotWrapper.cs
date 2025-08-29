using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Bot.Music.Cluster;
using Bot.Utilities.Logging;
using Common;
using Common.Config;
using Common.Infrastructure;
using Common.Utils;
using Discord;
using Discord.WebSocket;
using Enliven.MusicResolvers.Lavalink;
using Lavalink4NET;
using Lavalink4NET.Cluster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bot;

public class EnlivenBotWrapper(
    InstanceConfig instanceConfig,
    IConfiguration configuration,
    ILogger<EnlivenBotWrapper> globalLogger) {
    private TaskCompletionSource<bool>? _firstStartResult;

    /// <summary>
    /// Attempts to start a bot instance
    /// </summary>
    /// <returns>True if start successful, otherwise False</returns>
    public Task<bool> StartAsync(ILifetimeScope container, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) {
        if (_firstStartResult != null) throw new Exception("Current instance already started");

        _firstStartResult = new TaskCompletionSource<bool>();

        _ = RunLoopAsync(container, serviceProvider, cancellationToken);

        return _firstStartResult!.Task;
    }

    private async Task RunLoopAsync(ILifetimeScope container, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) {
        var topLevelHostedServices = serviceProvider.GetServices<IHostedService>()
            .ToImmutableArray();

        var isFirst = true;
        while (!cancellationToken.IsCancellationRequested) {
            var success = await Run(container, topLevelHostedServices, cancellationToken);
            if (isFirst && !success) {
                return;
            }

            isFirst = false;
        }
    }

    private async Task<bool> Run(ILifetimeScope container, ImmutableArray<IHostedService> topLevelHostedServices,
        CancellationToken cancellationToken) {
        var lifetimeScope = container.BeginLifetimeScope(Constants.BotLifetimeScopeTag, ConfigureBotLifetime);
        try {
            var logger = lifetimeScope.Resolve<ILogger<EnlivenBotWrapper>>();

            try {
                logger.LogInformation("Starting bot instance {InstanceName}",
                    Path.GetFileName(instanceConfig.Name));
                var bot = lifetimeScope.Resolve<EnlivenBot>();
                await bot.StartAsync(cancellationToken);

                var hostedServices = lifetimeScope
                    .Resolve<IReadOnlyCollection<IHostedService>>()
                    .Except(topLevelHostedServices)
                    .ToImmutableArray();
                foreach (var hostedService in hostedServices)
                    await hostedService.StartAsync(cancellationToken);

                _firstStartResult!.TrySetResult(true);

                try {
                    await bot.WaitForDisposeAsync();
                }
                catch (Exception) {
                    // ignored
                }

                try {
                    await hostedServices
                        .Select(service => service.StopAsync(CancellationToken.None))
                        .WhenAll();
                }
                catch (Exception) {
                    // ignored
                }

                logger.LogInformation("Stopping bot instance {InstanceName}", instanceConfig.Name);
            }
            catch (Exception e) {
                logger.LogError(e, "Failed to start bot instance with config {InstanceName}", instanceConfig.Name);
                _firstStartResult!.TrySetResult(false);
                return false;
            }
        }
        finally {
            async ValueTask DisposeBotLifetime() {
                try {
                    await lifetimeScope.DisposeAsync();
                }
                catch (Exception e) {
                    globalLogger.LogError(e, "Failed to dispose bot {InstanceName} lifetime", instanceConfig.Name);
                }
            }

            var timeout = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            var completed = await Task.WhenAny(Task.Run(DisposeBotLifetime, cancellationToken), timeout);
            if (completed == timeout) {
                globalLogger.LogError(
                    "Bot {InstanceName} lifetime did not disposed in time. Skipping and restarting",
                    instanceConfig.Name);
            }
        }

        return true;
    }

    private void ConfigureBotLifetime(ContainerBuilder builder) {
        var services = new ServiceCollection();
        services.AddSingleton(instanceConfig);
        services.AddSingleton<IServiceScopeFactory, ServiceScopeFactoryAdapter>();
        services.AddSingleton<EnlivenBot>();
        services.ConfigureNamedOptions<DiscordSocketConfig>(configuration);
        services.AddSingleton<EnlivenShardedClient>();
        services.AddSingleton<DiscordShardedClient>(s => s.GetRequiredService<EnlivenShardedClient>());
        services.AddSingleton<IDiscordClient>(s => s.GetRequiredService<EnlivenShardedClient>());
        services.AddLavalink();
        services.AddSingleton<IAudioService>(provider => provider.GetRequiredService<IEnlivenClusterAudioService>());
        services.AddSingleton<IEnlivenClusterAudioService, EnlivenClusterAudioService>();
        services.AddSingleton<IClusterAudioService>(provider =>
            provider.GetRequiredService<IEnlivenClusterAudioService>());
        services.AddPerBotServices();

        builder.Populate(services);
        builder.RegisterDecorator<ILoggerFactory>((_, _, factory) =>
            new BotInstanceLoggerFactoryDecorator(factory, instanceConfig));
    }
}