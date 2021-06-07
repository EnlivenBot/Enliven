using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extras.NLog;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Logging;
using Bot.DiscordRelated.MessageComponents;
using Bot.DiscordRelated.Music;
using Bot.Music.Spotify;
using Bot.Music.Yandex;
using Bot.Patches;
using Common;
using Common.Config;
using Common.Localization;
using Common.Music.Controller;
using Common.Music.Resolvers;
using Discord;
using Discord.WebSocket;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using YandexMusicResolver;

namespace Bot {
    internal class Program {
        private static async Task Main(string[] args) {
            InstallLogger();
            #if !DEBUG
            InstallErrorHandlers();
            #endif

            var containerBuilder = new ContainerBuilder();
            ConfigureServices(containerBuilder);
            Startup.ConfigureServices(containerBuilder);
            Container = containerBuilder.Build();

            using (var scope = Container.BeginLifetimeScope()) {
                var program = scope.Resolve<Program>();
                await program.Run();
            }

            Console.WriteLine("Execution end");
        }

        public static EnlivenShardedClient Client = null!;
        
        // ReSharper disable once InconsistentNaming
        private readonly ILogger logger;
        private IEnumerable<IService> _services;
        private IEnumerable<IPatch> _patches;
        private EnlivenConfig _config;

        public Program(ILogger logger, IEnumerable<IService> services, IEnumerable<IPatch> patches,
            EnlivenShardedClient discordShardedClient, EnlivenConfig config)
        {
            _config = config;
            config.Load();

            _patches = patches;
            _services = services;
            this.logger = logger;
            Client = discordShardedClient;
        }

        private static IContainer Container { get; set; } = null!;

        async Task Run()
        {
            logger.Info("Start Initialising");

            await Task.WhenAll(_patches.Select(patch => patch.Apply()).ToArray());
            await Task.WhenAll(_services.Select(service => service.OnPreDiscordLoginInitialize()).ToArray());

            Client.Log += OnClientLog;

            logger.Info("Start logining");
            var connectDelay = 30;
            while (true)
            {
                try
                {
                    await Client.LoginAsync(TokenType.Bot, _config.BotToken);
                    logger.Info("Successefully logged in");
                    break;
                }
                catch (Exception e)
                {
                    logger.Fatal(e, "Failed to login. Probably token is incorrect - {token}", _config.BotToken);
                    logger.Info("Waiting before next attempt - {delay}s", connectDelay);
                    await Task.Delay(TimeSpan.FromSeconds(connectDelay));
                    connectDelay += 10;
                }
            }

            LocalizationManager.Initialize();
            
            await Task.WhenAll(_services.Select(service => service.OnPreDiscordStartInitialize()).ToArray());

            await StartClient();

            await Task.WhenAll(_services.Select(service => service.OnPostDiscordStartInitialize()).ToArray());

            AppDomain.CurrentDomain.ProcessExit += async (sender, eventArgs) =>
            {
                await Client.SetStatusAsync(UserStatus.AFK);
                await Client.SetGameAsync("Reboot...");
            };

            await Task.Delay(-1);
        }

        public static void ConfigureServices(ContainerBuilder builder)
        {
            builder.AddEnlivenConfig();
            builder.RegisterType<MusicResolverService>().AsSelf().SingleInstance();
            builder.RegisterType<MusicController>().As<IMusicController>().SingleInstance();
            builder.RegisterType<ReliabilityService>().AsSelf();
            builder.RegisterModule<NLogModule>();
            builder.RegisterType<Program>().SingleInstance();
            builder.Register(context => new EnlivenShardedClient(new DiscordSocketConfig {MessageCacheSize = 100}))
                .AsSelf().As<DiscordShardedClient>().SingleInstance();

            builder.Register(context => context.Resolve<EnlivenConfig>().LavalinkNodes);

            // Discord type readers
            builder.RegisterType<ChannelFunctionTypeReader>().As<CustomTypeReader>();
            builder.RegisterType<LoopingStateTypeReader>().As<CustomTypeReader>();
            builder.RegisterType<BassBoostModeTypeReader>().As<CustomTypeReader>();

            // Database types
            builder.Register(context => context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                .GetCollection<SpotifyAssociation>(@"SpotifyAssociations")).SingleInstance();
            builder.Register(context => context.Resolve<LiteDatabaseProvider>().ProvideDatabase().GetAwaiter().GetResult()
                .GetCollection<MessageHistory>(@"MessageHistory")).SingleInstance();

            // Music resolvers
            builder.RegisterType<SpotifyMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SpotifyClientResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            
            builder.RegisterType<YandexClientResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<Music.Yandex.YandexMusicResolver>().AsSelf().AsImplementedInterfaces().SingleInstance();
            
            builder.RegisterType<SpotifyTrackEncoder>().AsSelf().AsImplementedInterfaces().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies).SingleInstance();
            builder.RegisterType<YandexTrackEncoder>().AsSelf().AsImplementedInterfaces().SingleInstance();

            // Providers
            builder.RegisterType<SpotifyAssociationProvider>().As<ISpotifyAssociationProvider>().SingleInstance();
            builder.RegisterType<MessageHistoryProvider>().As<IMessageHistoryProvider>().SingleInstance();
            builder.RegisterType<EmbedPlayerDisplayProvider>().SingleInstance();

            // Services
            builder.RegisterType<CustomCommandService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageHistoryService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<GlobalBehaviorsService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<ReliabilityService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<CommandHandlerService>().As<IService>().AsSelf().SingleInstance();
            builder.RegisterType<StatisticsService>().As<IStatisticsService>().AsSelf().SingleInstance();
            builder.RegisterType<MessageComponentService>().As<MessageComponentService>().AsSelf().SingleInstance();
        }

        public async Task StartClient()
        {
            logger.Info("Starting client");
            await Client.StartAsync();
            await Client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
        }

        private Task OnClientLog(LogMessage message) {
            if (message.Message != null && message.Message.StartsWith("Unknown Dispatch")) {
                return Task.CompletedTask;
            }

            logger.Log(message.Severity, message.Exception, "{message} from {source}", message.Message!, message.Source);
            return Task.CompletedTask;
        }

        private static void InstallLogger()
        {
            var logsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(logsFolder);

            foreach (var file in Directory.GetFiles(logsFolder, "*.log"))
            {
                try
                {
                    using var fs = File.Create(Path.ChangeExtension(file, ".zip"));
                    using var zip = new ZipOutputStream(fs);
                    zip.SetLevel(9);
                    var zipEntry = new ZipEntry(Path.GetFileName(file));
                    var fileInfo = new FileInfo(file);
                    zipEntry.Size = fileInfo.Length;
                    zipEntry.DateTime = fileInfo.LastWriteTime;
                    zip.PutNextEntry(zipEntry);
                    var buffer = new byte[4096];
                    using (var fsInput = File.OpenRead(file))
                    {
                        StreamUtils.Copy(fsInput, zip, buffer);
                    }

                    zip.CloseEntry();
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            var config = new LoggingConfiguration();

            var layout =
                Layout.FromString(
                    "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");
            // Targets where to log to: File and Console
            var logfile = new FileTarget("logfile")
            {
                FileName = Path.Combine(Directory.GetCurrentDirectory(), "Logs",
                    DateTime.Now.ToString("yyyyMMddTHHmmss") + ".log"),
                Layout = layout
            };
            var logconsole = new ColoredConsoleTarget("logconsole") {Layout = layout};

            // Rules for mapping loggers to targets
#if DEBUG
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
#endif
#if !DEBUG
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
#endif

            // Apply config           
            LogManager.Configuration = config;
        }

        // ReSharper disable once UnusedMember.Local
        private static void InstallErrorHandlers()
        {
            var logger = LogManager.GetLogger("Global");
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                logger.Fatal(args.ExceptionObject as Exception, "Global uncaught exception");
            TaskScheduler.UnobservedTaskException += (sender, args) =>
                logger.Fatal(args.Exception?.Flatten(), "Global uncaught task exception");
        }
    }
}