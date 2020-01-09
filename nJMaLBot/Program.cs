using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Config;
using Bot.Music;
using Discord;
using Discord.WebSocket;

namespace Bot {
    internal class Program {
        public static DiscordSocketClient Client;
        public static CommandHandler Handler;
        public static event EventHandler<DiscordSocketClient> OnClientConnect;

        private static void Main(string[] args) {
            Console.WriteLine("Start Initialising");

            Console.WriteLine("Starting Bot");
            RuntimeHelpers.RunClassConstructor(typeof(MessageHistoryManager).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(MusicUtils).TypeHandle);
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
            ConsoleCommandsHandler();
        }

        private static async Task MainAsync(string[] args) {
            var config = new DiscordSocketConfig {MessageCacheSize = 100};
            Client = new DiscordSocketClient(config);

            Console.WriteLine("Start authorization");


            Client.ReactionAdded += ReactionAdded;
            Client.Ready += async () => {
                Console.WriteLine("Bot has connected!");
                OnClientConnect?.Invoke(null, Client);
            };

            Client.Disconnected += exception => Client_Disconnected(exception, args);

            await Client.LoginAsync(TokenType.Bot, GlobalConfig.Instance.BotToken);
            await Client.StartAsync();

            Handler = new CommandHandler();
            await Handler.Install(Client);
        }

        private static async Task Client_Disconnected(Exception e, string[] args) {
            Client.Dispose();
            Console.WriteLine("================================");
            Console.WriteLine("Bot Disconnected with exception:");
            Console.WriteLine(e.ToString());
            Console.WriteLine("\nReconnecting...\n\n");
            await MainAsync(args);
        }

        public static void ConsoleCommandsHandler() {
            while (true) { }
        }

        private static async Task ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3) {
            if (arg3.Emote.ToString() != "📖")
                return;
            await ((IUserMessage) ((ISocketMessageChannel) Client.GetChannel(arg2.Id)).GetMessageAsync(arg3.MessageId).Result).RemoveReactionAsync(
                new Emoji("📖"), arg3.User.Value);

            await MessageHistoryManager.PrintLog(arg1.Id, arg2.Id, (SocketTextChannel) arg2, (IGuildUser) arg3.User.Value);
        }
    }
}