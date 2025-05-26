using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac.Extensions.DependencyInjection;
using Bot;
using Bot.DiscordRelated.Commands;
using Bot.Music.Deezer;
using Bot.Music.Spotify;
using Bot.Utilities;
using Bot.Utilities.Logging;
using Common;
using Common.Config;
using Common.Localization;
using Common.Music.Resolvers;
using Common.Utils;
using Lavalink4NET.Artwork;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogTracing;

var builder = WebApplication.CreateBuilder(args)
    .AddServiceDefaults();

// using var listener = new ActivityListenerConfiguration().TraceToSharedLogger();

builder.Host
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureServices(services =>
    {
        services.AddSerilog((s, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.CustomLogLevelFromConfiguration(builder.Configuration)
            .ReadFrom.Services(s)
            .Enrich.FromLogContext());
        services.AddHostedService<Worker>();
        services.AddHttpClient();
        services.AddSingleton<HttpClient>();
        services.AddDatabase();

        services.AddSingleton<IArtworkService, ArtworkService>();
        services.AddSingleton<CustomTypeReader, LoopingStateTypeReader>();
        services.AddSingleton<MusicResolverService, MusicResolverService>();

        services.AddVk(builder.Configuration);
        services.AddYandex(builder.Configuration);
        services.AddSingleton<IMusicResolver, DeezerMusicResolver>();
        services.ConfigureNamedOptions<SpotifyCredentials>(builder.Configuration);
        services.AddSingleton<IMusicResolver, SpotifyMusicResolver>();
    });

var app = builder.Build();
app.UseSerilogRequestLogging();

StaticLogger.Setup(app.Services.GetRequiredService<ILoggerFactory>());
AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    app.Logger.LogError(args.ExceptionObject as Exception, "Global uncaught exception");
TaskScheduler.UnobservedTaskException += (_, args) =>
{
    app.Logger.LogError(args.Exception?.Flatten(), "Global uncaught task exception");
    args.SetObserved();
};

// Initializing localization
Directory.CreateDirectory("Config");
LocalizationManager.Initialize();

Log.Logger.Information("My awesome log from {Date}, {Time}", DateTime.Now, DateTime.Now);
app.MapGet("/", () => "Enliven web host started");
var endpointProviders = app.Services.GetServices<IEndpointProvider>();
await Task.WhenAll(endpointProviders.Select(provider => provider.ConfigureEndpoints(app)));

await app.RunAsync();

await Log.CloseAndFlushAsync();