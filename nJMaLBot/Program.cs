using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Config;
using Bot.Utilities;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Bot
{
    class Program
    {
        public static DiscordSocketClient Client;
        public static CommandHandler      Handler;
        public static Config.GlobalConfig GlobalConfig;


        static void Main(string[] args) {
            Console.WriteLine("Start Initialising");

            GlobalConfig = GlobalConfig.LoadConfig();
            ChannelUtils.LoadCache();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => ChannelUtils.SaveCache();

            Console.WriteLine("Starting Bot");
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args) {
            var _config = new DiscordSocketConfig {MessageCacheSize = 100};
            Client = new DiscordSocketClient(_config);
            await Client.LoginAsync(TokenType.Bot, GlobalConfig.BotToken);
            await Client.StartAsync();

            Client.MessageUpdated += MessageUpdated;
            Client.MessageReceived += MessageReceived;
            Client.MessageDeleted += MessageDeleted;
            Client.ReactionAdded += ReactionAdded;
            Client.Ready += () => {
                                Console.WriteLine("Bot is connected!");
                                return Task.CompletedTask;
                            };

            Handler = new CommandHandler();
            await Handler.Install(Client);

            await Task.Delay(-1);
        }

        private static async Task ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3) {
            if (arg3.Emote.ToString() != "📖")
                return;
            await ((IUserMessage) ((ISocketMessageChannel) Client.GetChannel(arg2.Id)).GetMessageAsync(arg3.MessageId).Result).RemoveReactionAsync(new Emoji("📖"), arg3.User.Value);
            await arg2.SendMessageAsync("", false, MessageStorage.Load((arg2 as SocketGuildChannel).Guild.Id, arg2.Id, arg3.MessageId).BuildEmbed());
        }

        private static async Task MessageDeleted(Cacheable<IMessage, ulong> arg1, ISocketMessageChannel arg2) {
            MessageStorage thisMessage = MessageStorage.Load((arg2 as SocketGuildChannel).Guild.Id, arg2.Id, arg1.Id);
            if (thisMessage != null) {
                thisMessage.DeleteMessage();
                var channel = (ISocketMessageChannel) Client.GetChannel(ChannelUtils.GetChannel((arg2 as SocketGuildChannel).Guild.Id, ChannelUtils.ChannelFunction.Log));
                EmbedBuilder eb = new EmbedBuilder()
                                 .WithFields(thisMessage.BuildLog())
                                 .WithAuthor(thisMessage.GetAuthorName(), thisMessage.GetAuthorIcon())
                                 .WithDescription($"написал сообщение в <#{thisMessage.ChannelId}>, которое было только что удалено.\n" +
                                                  "Вот история его изменений:")
                                 .WithFooter($"ID сообщения: {thisMessage.Id}")
                                 .WithTimestamp(DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3)))
                                 .WithColor(Color.Red);
                await channel.SendMessageAsync(null, false, eb.Build());
            }
            else {
                var channel = (ISocketMessageChannel) Client.GetChannel(ChannelUtils.GetChannel((arg2 as SocketGuildChannel).Guild.Id, ChannelUtils.ChannelFunction.Log));
                EmbedBuilder eb = new EmbedBuilder()
                                 .AddField("Удалённое сообщение:", "```Отсутствует```")
                                 .WithDescription($"Сообщение было удалено в <#{arg2.Id}>")
                                 .WithFooter($"ID сообщения: {arg1.Id}")
                                 .WithTimestamp(DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3)))
                                 .WithColor(Color.DarkRed);
                await channel.SendMessageAsync(null, false, eb.Build());
            }
        }

        private static Task MessageReceived(SocketMessage arg) {
            if (arg.Author.IsBot || arg.Author.IsWebhook) return Task.CompletedTask;
            var channel = arg.Channel;
            MessageStorage thisMessage = new MessageStorage() {
                                                                  AuthorId = arg.Author.Id, AuthorAvatar = arg.Author.GetAvatarUrl(), AuthorName = arg.Author.Username,
                                                                  Id = arg.Id, ChannelId = arg.Channel.Id, GuildId = (channel as SocketGuildChannel).Guild.Id,
                                                                  CreationDate = arg.CreatedAt, UrlToNavigate = arg.GetJumpUrl(),
                                                                  Edits = new List<MessageStorage.MessageSnapshot>()
                                                                          {new MessageStorage.MessageSnapshot() {Content = arg.Content, EditTimestamp = arg.CreatedAt}}
                                                              };
            thisMessage.Save();
            return Task.CompletedTask;
        }

        private static async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel) {
            if (after.Author.IsBot || after.Author.IsWebhook) return;

            MessageStorage thisMessage = MessageStorage.Load((channel as SocketGuildChannel).Guild.Id, channel.Id, after.Id) ??
                                         new MessageStorage() {
                                                                  AuthorId = after.Author.Id, AuthorAvatar = after.Author.GetAvatarUrl(), AuthorName = after.Author.Username,
                                                                  Id = after.Id, ChannelId = channel.Id, GuildId = (channel as SocketGuildChannel).Guild.Id,
                                                                  CreationDate = after.CreatedAt, UrlToNavigate = after.GetJumpUrl()
                                                              };
            if (thisMessage.Edits.Count == 0) {
                var message = await before.GetOrDownloadAsync();
                thisMessage.Edits.Add(new MessageStorage.MessageSnapshot() {
                                                                               Content = message.Content          != after.Content ? message.Content : "***Предыдущая запись недоступна",
                                                                               EditTimestamp = message.ToString() != after.ToString() ? message.Timestamp : message.CreatedAt
                                                                           });
            }

            thisMessage.Edits.Add(new MessageStorage.MessageSnapshot() {Content = after.Content, EditTimestamp = after.EditedTimestamp});

            thisMessage.Save();

            string consoleOutput = "#################################\n";
            consoleOutput += $"{(channel as SocketGuildChannel).Guild.Name} - {channel.Name} - by {after.Author.Username}\n";
            consoleOutput += string.Join("\n", thisMessage.Edits.Select(x => $"{x.EditTimestamp.ToString()}\n  {x.Content.Replace("\n", "\n  ")}"));
            consoleOutput += "\n";
            Console.WriteLine(consoleOutput);
        }

        private Process CreateStream(string path) {
            return Process.Start(new ProcessStartInfo {
                                                          FileName = "ffmpeg.exe",
                                                          Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                                                          UseShellExecute = false,
                                                          RedirectStandardOutput = true,
                                                      });
        }

        private async Task SendAsync(IAudioClient client, string path) {
            // Create FFmpeg using the previous example
            using (var ffmpeg = CreateStream(path))
                using (var output = ffmpeg.StandardOutput.BaseStream)
                    using (var discord = client.CreatePCMStream(AudioApplication.Mixed)) {
                        try {
                            await output.CopyToAsync(discord);
                        }
                        finally {
                            await discord.FlushAsync();
                        }
                    }
        }
    }
}
