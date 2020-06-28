using System;
using System.IO;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Logging;
using Bot.Music;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Commands;
using CommandLine;
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
        // ReSharper disable once InconsistentNaming
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        // ReSharper disable once NotAccessedField.Local
        private static ReliabilityService _reliabilityService = null!;
        // ReSharper disable once InconsistentNaming
        private static readonly TaskCompletionSource<bool> waitStartSource = new TaskCompletionSource<bool>();
        public static Task WaitStartAsync = waitStartSource.Task;
        public static CmdOptions CmdOptions = null!;

        private static void Main(string[] args) {
            Parser.Default.ParseArguments<CmdOptions>(args).WithParsed(options => {
                CmdOptions = options;
                if (CmdOptions.BotToken != null) {
                    GlobalConfig.Instance.BotToken = CmdOptions.BotToken;
                }
            });
            InstallLogger();
            #if !DEBUG
            InstallErrorHandlers();
            #endif
            logger.Info("Start Initialising");

            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        // ReSharper disable once UnusedParameter.Local
        private static async Task MainAsync(string[] args) {
            var config = new DiscordSocketConfig {MessageCacheSize = 100};
            Client = new DiscordShardedClient(config);

            Task OnClientLog(LogMessage message) {
                logger.Log(message.Severity, message.Exception, "{message} from {source}", message.Message, message.Source);
                return Task.CompletedTask;
            }

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
            
            Localization.Initialize();
            GlobalDB.Initialize();

            logger.Info("Starting client");
            await Client.StartAsync();
            await Client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
            _reliabilityService = new ReliabilityService(Client, OnClientLog);
            waitStartSource.SetResult(true);

            Handler = await CommandHandler.Create(Client);
            
            AppDomain.CurrentDomain.ProcessExit += async (sender, eventArgs) => {
                await Client.SetStatusAsync(UserStatus.AFK);
                await Client.SetGameAsync("Reboot...");
            };
            
            MessageHistoryManager.Initialize();
            MusicUtils.Initialize();
            await Task.Delay(-1);
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

        private static void InstallErrorHandlers() {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => logger.Fatal(args.ExceptionObject as Exception, "Global uncaught exception");
            TaskScheduler.UnobservedTaskException += (sender, args) => logger.Fatal(args.Exception?.Flatten(), "Global uncaught task exception");
        }
    }
}