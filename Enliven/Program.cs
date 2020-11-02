using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extras.NLog;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Logging;
using Bot.Utilities.Music;
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

namespace Bot {
    internal class Program {
        public static DiscordShardedClient Client = null!;
        public static CommandHandler Handler = null!;

        // ReSharper disable once NotAccessedField.Local
        private static ReliabilityService _reliabilityService = null!;

        // ReSharper disable once UnusedParameter.Local
        private static bool _clientStarted;

        // ReSharper disable once InconsistentNaming
        private readonly ILogger logger;

        public Program(ILogger logger) {
            this.logger = logger;
        }

        private static IContainer Container { get; set; }

        // ReSharper disable once UnusedParameter.Local
        private static async Task Main(string[] args) {
            InstallLogger();
            #if !DEBUG
            InstallErrorHandlers();
            #endif

            var containerBuilder = new ContainerBuilder();
            ConfigureServices(containerBuilder);
            Container = containerBuilder.Build();

            using (var scope = Container.BeginLifetimeScope()) {
                var program = scope.Resolve<Program>();
                await program.Run();
            }
            
            Console.WriteLine("Execution end");
        }

        async Task Run() {
            logger.Info("Start Initialising");

            var config = new DiscordSocketConfig {MessageCacheSize = 100};
            Client = new DiscordShardedClient(config);
            Client.Log += OnClientLog;

            logger.Info("Start logining");
            var connectDelay = 30;
            while (true) {
                try {
                    await Client.LoginAsync(TokenType.Bot, GlobalConfig.Instance.BotToken);
                    logger.Info("Successefully logged in");
                    break;
                }
                catch (Exception e) {
                    logger.Fatal(e, "Failed to login. Probably token is incorrect - {token}", GlobalConfig.Instance.BotToken);
                    logger.Info("Waiting before next attempt - {delay}s", connectDelay);
                    await Task.Delay(TimeSpan.FromSeconds(connectDelay));
                    connectDelay += 10;
                }
            }

            LocalizationManager.Initialize();
            Database.Initialize();

            await StartClient();

            Handler = await CommandHandler.Create(Client);

            AppDomain.CurrentDomain.ProcessExit += async (sender, eventArgs) => {
                await Client.SetStatusAsync(UserStatus.AFK);
                await Client.SetGameAsync("Reboot...");
            };

            MessageHistoryManager.Initialize();
            SpotifyMusicResolver.Initialize();
            Patch.ApplyUsersPatch();
            await Task.Delay(-1);
        }

        public static void ConfigureServices(ContainerBuilder builder)
        {
            builder.RegisterType<MusicResolverService>().AsSelf().SingleInstance();
            builder.RegisterType<MusicController>().As<IMusicController>().SingleInstance();
            builder.RegisterType<ReliabilityService>().AsSelf();
            builder.RegisterModule<NLogModule>();
            builder.RegisterType<Program>();
        }

        public async Task StartClient() {
            if (_clientStarted) return;
            _clientStarted = true;
            logger.Info("Starting client");
            await Client.StartAsync();
            await Client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
            _reliabilityService = new ReliabilityService(Client, OnClientLog);
        }

        private Task OnClientLog(LogMessage message) {
            logger.Log(message.Severity, message.Exception, "{message} from {source}", message.Message, message.Source);
            return Task.CompletedTask;
        }

        private static void InstallLogger() {
            var logsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(logsFolder);

            foreach (var file in Directory.GetFiles(logsFolder, "*.log")) {
                try {
                    using var fs = File.Create(Path.ChangeExtension(file, ".zip"));
                    using var zip = new ZipOutputStream(fs);
                    zip.SetLevel(9);
                    var zipEntry = new ZipEntry(Path.GetFileName(file));
                    var fileInfo = new FileInfo(file);
                    zipEntry.Size = fileInfo.Length;
                    zipEntry.DateTime = fileInfo.LastWriteTime;
                    zip.PutNextEntry(zipEntry);
                    var buffer = new byte[4096];
                    using (var fsInput = File.OpenRead(file)) {
                        StreamUtils.Copy(fsInput, zip, buffer);
                    }

                    zip.CloseEntry();
                    File.Delete(file);
                }
                catch (Exception) {
                    // ignored
                }
            }

            var config = new LoggingConfiguration();

            var layout = Layout.FromString("${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");
            // Targets where to log to: File and Console
            var logfile = new FileTarget("logfile") {
                FileName = Path.Combine(Directory.GetCurrentDirectory(), "Logs", DateTime.Now.ToString("yyyyMMddTHHmmss") + ".log"),
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
        private void InstallErrorHandlers() {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => logger.Fatal(args.ExceptionObject as Exception, "Global uncaught exception");
            TaskScheduler.UnobservedTaskException += (sender, args) => logger.Fatal(args.Exception?.Flatten(), "Global uncaught task exception");
        }
    }
}