using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Extras.NLog;
using Bot;
using Bot.DiscordRelated.Commands;
using Bot.Music.Deezer;
using Bot.Music.Spotify;
using Bot.Utilities;
using Common;
using Common.Config;
using Common.Localization;
using Common.Music.Resolvers;
using Lavalink4NET.Artwork;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using NLog.Extensions.Logging;

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
        services.AddLogging(loggingBuilder => loggingBuilder.AddNLog());
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
    })
    .ConfigureContainer<ContainerBuilder>(b => b.RegisterModule<NLogModule>());

var app = builder.Build();

app.MapGet("/", () => "Enliven web host started");
var endpointProviders = app.Services.GetServices<IEndpointProvider>();
await Task.WhenAll(endpointProviders.Select(provider => provider.ConfigureEndpoints(app)));

await app.RunAsync();