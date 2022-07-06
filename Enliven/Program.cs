using System;
using System.IO;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Bot;
using Common;
using Common.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;

// Initializing localization
Directory.CreateDirectory("Config");
LocalizationManager.Initialize();

// Setting up global handlers
var logger = LogManager.GetLogger("Global");
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
    logger.Fatal(args.ExceptionObject as Exception, "Global uncaught exception");
TaskScheduler.UnobservedTaskException += (sender, args) => {
    logger.Fatal(args.Exception?.Flatten(), "Global uncaught task exception");
    args.SetObserved();
};

// Creating and running host
IHost host = Host.CreateDefaultBuilder(args)
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureServices(services => {
        services.AddHostedService<Worker>();
    })
    .ConfigureContainer<ContainerBuilder>(container => {
        container
            .AddGlobalConfig()
            .AddEnlivenServices()
            .AddCommonServices();
    })
    .ConfigureLogging(builder => builder.ClearProviders())
    .Build();

await host.RunAsync();