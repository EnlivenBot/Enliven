using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Config;
using Bot.Music;
using Bot.Utilities.Collector;
using Bot.Utilities.Emoji;
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
        public static event EventHandler<DiscordSocketClient> OnClientConnect;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static int connectDelay = -1;

        private static void Main(string[] args) {
            InstallLogger();
            #if !DEBUG
            InstallErrorHandlers();
            #endif
            logger.Info("Start Initialising");

            RuntimeHelpers.RunClassConstructor(typeof(MessageHistoryManager).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(MusicUtils).TypeHandle);
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
            ConsoleCommandsHandler();
        }

        private static async Task MainAsync(string[] args) {
            var config = new DiscordSocketConfig {MessageCacheSize = 100};
            Client = new DiscordSocketClient(config);

            logger.Info("Start authorization");


            Client.Ready += async () => {
                await Client.SetGameAsync("mentions of itself to get started", null, ActivityType.Listening);
                OnClientConnect?.Invoke(null, Client);
            };

            Client.Disconnected += exception => Client_Disconnected(exception, args);

            login:
            try {
                logger.Info("Trying to login");
                await Client.LoginAsync(TokenType.Bot, GlobalConfig.Instance.BotToken);
                connectDelay = 0;
            }
            catch (Exception e) {
                logger.Fatal(e, "Failed to connect. Probably token is incorrect - {token}", GlobalConfig.Instance.BotToken);
                if (connectDelay == -1) Environment.Exit(-1);
                logger.Info("Waiting before next attempt - {delay}s", connectDelay);
                await Task.Delay(TimeSpan.FromSeconds(connectDelay));
                connectDelay += 5;
                goto login;
            }

            await Client.StartAsync();
            MessageHistoryManager.SetEmojiHandlers();

            Handler = new CommandHandler();
            await Handler.Install(Client);
        }

        private static async Task Client_Disconnected(Exception e, string[] args) {
            Client.Dispose();
            logger.Warn(e, "Client disconnected");
            logger.Info("Retrying");
            await MainAsync(args);
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
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
            #endif
            #if !DEBUG
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logfile);
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