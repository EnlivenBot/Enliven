using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Bot;
using Bot.Utilities;
using Common;
using Common.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;

// Initializing localization
Directory.CreateDirectory("Config");
LocalizationManager.Initialize();

// Setting up global handlers
var logger = LogManager.GetLogger("Global");
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
    logger.Fatal(args.ExceptionObject as Exception, "Global uncaught exception");
TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    logger.Fatal(args.Exception?.Flatten(), "Global uncaught task exception");
    args.SetObserved();
};

// Creating and running host

var builder = WebApplication.CreateBuilder(args);
builder.Host
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddHttpClient();
    })
    .ConfigureContainer<ContainerBuilder>(container =>
    {
        container
            .AddEnlivenServices()
            .AddCommonServices()
            .AddYandexResolver()
            .AddVk();
    });

var app = builder.Build();

app.MapGet("/", () => "Enliven web host started");
var endpointProviders = app.Services.GetServices<IEndpointProvider>();
await Task.WhenAll(endpointProviders.Select(provider => provider.ConfigureEndpoints(app)));

await app.RunAsync();