using System;
using System.IO;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Config;
using Common.Utils;
using Lavalink4NET;
using Microsoft.Extensions.Configuration;
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
    public Task<bool> StartAsync(ILifetimeScope container, CancellationToken cancellationToken)
    {
        if (_firstStartResult != null) throw new Exception("Current instance already started");

        _firstStartResult = new TaskCompletionSource<bool>();

        _ = RunLoopAsync(container, cancellationToken);

        return _firstStartResult!.Task;
    }

    private async Task RunLoopAsync(ILifetimeScope container, CancellationToken cancellationToken)
    {
        var isFirst = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var lifetimeScope =
                    container.BeginLifetimeScope(Constants.BotLifetimeScopeTag, ConfigureBotLifetime);
                var aas = lifetimeScope.Resolve<IAudioService>();
                var bot = lifetimeScope.Resolve<EnlivenBot>();
                await bot.StartAsync();
                _firstStartResult!.TrySetResult(true);

                try
                {
                    await bot.Disposed.ToTask(cancellationToken);
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
        builder.Register(context => _instanceConfig)
            .AsSelf()
            .SingleInstance();
        builder.Register(context => _instanceConfig)
            .AsSelf()
            .AsImplementedInterfaces()
            .SingleInstance();
        builder.RegisterType<ServiceScopeFactoryAdapter>()
            .AsImplementedInterfaces()
            .SingleInstance();
        builder.AddLavalink(_instanceConfig);
    }
}