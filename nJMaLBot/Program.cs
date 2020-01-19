using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Config;
using Bot.Music;
using Discord;
using Discord.WebSocket;
using NLog;
using NLog.Layouts;

namespace Bot {
    internal class Program {
        public static DiscordSocketClient Client;
        public static CommandHandler Handler;
        public static event EventHandler<DiscordSocketClient> OnClientConnect;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static void Main(string[] args) {
            InstallLogger();
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


            Client.ReactionAdded += ReactionAdded;
            Client.Ready += async () => {
                logger.Info("Bot has connected!");
                OnClientConnect?.Invoke(null, Client);
            };

            Client.Disconnected += exception => Client_Disconnected(exception, args);

            try {
                await Client.LoginAsync(TokenType.Bot, GlobalConfig.Instance.BotToken);
            }
            catch (Exception e) {
                logger.Fatal(e, "Using non valid bot token - {token}", GlobalConfig.Instance.BotToken);
                Environment.Exit(-1);
            }

            await Client.StartAsync();

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

        private static async Task ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3) {
            if (arg3.Emote.ToString() != "📖")
                return;
            await ((IUserMessage) ((ISocketMessageChannel) Client.GetChannel(arg2.Id)).GetMessageAsync(arg3.MessageId).Result).RemoveReactionAsync(
                new Emoji("📖"), arg3.User.Value);

            await logger.Swallow(async () => await MessageHistoryManager.PrintLog(arg1.Id, arg2.Id, (SocketTextChannel) arg2, (IGuildUser) arg3.User.Value));
        }

        private static void InstallLogger() {
            var config = new NLog.Config.LoggingConfiguration();

            var layout = Layout.FromString("${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");
            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") {
                FileName = Path.Combine("Logs", DateTime.Now.ToString("yyyyMMddTHHmmss") + ".log"),
                Layout = layout
            };
            var logconsole = new NLog.Targets.ColoredConsoleTarget("logconsole") {Layout = layout};

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            // Apply config           
            NLog.LogManager.Configuration = config;
        }
    }
}