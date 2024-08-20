using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Common.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NLog;
using Tyrrrz.Extensions;

namespace Bot;

public class Worker : BackgroundService
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private readonly IConfiguration _configuration;

    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILifetimeScope _lifetimeScope;
    private readonly IServiceProvider _serviceProvider;

    public Worker(IHostApplicationLifetime hostApplicationLifetime, ILifetimeScope lifetimeScope,
        IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _lifetimeScope = lifetimeScope;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _lifetimeScope.Disposer.AddInstanceForDisposal(this);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var configs = _configuration.GetSection("Instances").Get<IEnumerable<InstanceConfig>>()
            ?.ToList();
        if (configs!.IsNullOrEmpty())
            throw new InvalidOperationException("No bot instances configured. Check your appsettings");
        var enlivenBotWrappers = configs!.Select(config => new EnlivenBotWrapper(config, _configuration));
        var startTasks = enlivenBotWrappers
            .Select(wrapper => wrapper.StartAsync(_lifetimeScope, _serviceProvider, CancellationToken.None)).ToList();

        var whenAll = await Task.WhenAll(startTasks);
        if (whenAll.All(b => !b))
        {
            // If all failed - exit
            Logger.Fatal("All bot instances failed to start. Check configs, logs and try again");
            Environment.Exit(-1);
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.Fatal("Recieved application stop request. Stopping");
        await _lifetimeScope.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _hostApplicationLifetime.StopApplication();
        base.Dispose();
    }
}