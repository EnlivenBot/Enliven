using System;
using System.IO;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Config;
using Bot.Music;
using Discord;
using Discord.WebSocket;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using NLog;
using NLog.Layouts;

namespace Bot {
    internal class Program {
        public static DiscordSocketClient Client;
        public static CommandHandler Handler;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static void Main(string[] args) {
            InstallLogger();
            #if !DEBUG
            InstallErrorHandlers();
            #endif
            logger.Info("Start Initialising");

            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
            ConsoleCommandsHandler();
        }

        private static async Task MainAsync(string[] args) {
            var config = new DiscordSocketConfig {MessageCacheSize = 100};
            Client = new DiscordSocketClient(config);
            Client.Log += message => {
                var logLevel = message.Severity switch {
                    LogSeverity.Critical => LogLevel.Fatal,
                    LogSeverity.Error    => LogLevel.Error,
                    LogSeverity.Warning  => LogLevel.Warn,
                    LogSeverity.Info     => LogLevel.Info,
                    LogSeverity.Verbose  => LogLevel.Debug,
                    LogSeverity.Debug    => LogLevel.Trace,
                    _                    => throw new ArgumentOutOfRangeException()
                };
                logger.Log(logLevel, message.Exception, "{message} from {source}", message.Message, message.Source);
                return Task.CompletedTask;
            };
            
            Client.Ready += async () => {
                logger.Info("Successefully started client");
                await Client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
            };

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

            logger.Info("Starting client");
            await Client.StartAsync();
            
            Handler = new CommandHandler();
            await Handler.Install(Client);
            
            MessageHistoryManager.SetHandlers();
            await MusicUtils.SetHandler();
        }

        public static void ConsoleCommandsHandler() {
            while (true) {
                var input = Console.ReadLine();
            }
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
                catch (Exception e) {
                    // ignored
                }
            }

            var config = new NLog.Config.LoggingConfiguration();

            var layout = Layout.FromString("${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");
            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") {
                FileName = Path.Combine(Directory.GetCurrentDirectory(), "Logs", DateTime.Now.ToString("yyyyMMddTHHmmss") + ".log"),
                Layout = layout
            };
            var logconsole = new NLog.Targets.ColoredConsoleTarget("logconsole") {Layout = layout};

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
            NLog.LogManager.Configuration = config;
        }

        private static void InstallErrorHandlers() {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => logger.Fatal(args.ExceptionObject as Exception, "Global uncaught exception");
            TaskScheduler.UnobservedTaskException += (sender, args) => logger.Fatal(args.Exception.Flatten(), "Global uncaught task exception");
        }
    }
}