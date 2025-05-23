﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tyrrrz.Extensions;

namespace Bot;

public class Worker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;

    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILifetimeScope _lifetimeScope;
    private readonly IServiceProvider _serviceProvider;

    public Worker(IHostApplicationLifetime hostApplicationLifetime, ILifetimeScope lifetimeScope,
        IServiceProvider serviceProvider, IConfiguration configuration, ILogger<Worker> logger)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _lifetimeScope = lifetimeScope;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _lifetimeScope.Disposer.AddInstanceForDisposal(this);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configs = _configuration.GetSection("Instances")
            .Get<IEnumerable<InstanceConfig>>()
            ?.ToList();
        if (configs!.IsNullOrEmpty())
            throw new InvalidOperationException("No bot instances configured. Check your appsettings");
        var enlivenBotWrappers = configs!.Select(config => 
            new EnlivenBotWrapper(config, _configuration));
        
        var startResults = await enlivenBotWrappers
            .Select(wrapper => wrapper.StartAsync(_lifetimeScope, _serviceProvider, CancellationToken.None))
            .WhenAll()
            .WaitAsync(stoppingToken);

        if (startResults.All(b => !b))
        {
            // If all failed - exit
            _logger.LogCritical("All bot instances failed to start. Check configs, logs and try again");
            _hostApplicationLifetime.StopApplication();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogError("Received application stop request. Stopping");
        var lifetimeDisposeTask = _lifetimeScope.DisposeAsync();
        await base.StopAsync(cancellationToken);
        await lifetimeDisposeTask;
    }

    public override void Dispose()
    {
        _hostApplicationLifetime.StopApplication();
        base.Dispose();
    }
}