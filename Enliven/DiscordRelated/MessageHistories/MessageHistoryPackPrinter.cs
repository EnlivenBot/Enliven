using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Localization.Providers;
using Discord;
using Discord.WebSocket;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.MessageHistories {
    public class MessageHistoryPackPrinter {
        private readonly EnlivenShardedClient _discordClient;
        private ConcurrentDictionary<ulong, PackDataInChannel> _datas = new ConcurrentDictionary<ulong, PackDataInChannel>();
        public MessageHistoryPackPrinter(EnlivenShardedClient discordClient) {
            _discordClient = discordClient;
            _discordClient.MessageReceived += OnMessageRecieved;
        }

        public IMessageSendData GeneratePack(ITextChannel deletedInChannel, ILocalizationProvider loc) {
            return new InfoAboutDeletion(this, deletedInChannel.Mention, loc);
        }

        private PackDataInChannel GetPackDataForChannel(IMessageChannel channel, ILocalizationProvider loc) {
            return _datas.GetOrAdd(channel.Id, arg => new PackDataInChannel(channel, loc, _discordClient));
        }
        
        private Task OnMessageRecieved(SocketMessage arg) {
            if (_datas.TryGetValue(arg.Channel.Id, out var packData)) {
                packData!.OnNewMessageRecieved(arg.Id);
            }
            
            return Task.CompletedTask;
        }
        
        private class InfoAboutDeletion : IMessageSendData {
            private readonly MessageHistoryPackPrinter _messageHistoryPackPrinter;
            private readonly string _mention;
            private readonly ILocalizationProvider _loc;
            private readonly DateTimeOffset _deletionTime;
            public InfoAboutDeletion(MessageHistoryPackPrinter messageHistoryPackPrinter, string mention, ILocalizationProvider loc) {
                _loc = loc;
                _mention = mention;
                _messageHistoryPackPrinter = messageHistoryPackPrinter;
                _deletionTime = DateTimeOffset.Now;
            }
            public Task<IUserMessage> SendMessage(IMessageChannel targetChannel) {
                var packData = _messageHistoryPackPrinter.GetPackDataForChannel(targetChannel, _loc);
                packData.EnqueueEntry(_deletionTime, _mention);
                return packData.ModifyOrSendMessage();
            }
        }

        private class PackDataInChannel {
            public PackDataInChannel(IMessageChannel channel, ILocalizationProvider localizationProvider, EnlivenShardedClient enlivenShardedClient) {
                _localizationProvider = localizationProvider;
                _channel = channel;
                _currentMessageTask = Task.Run(async () => {
                    var messages = await _channel.GetMessagesAsync(1).FlattenAsync();
                    if (messages.FirstOrDefault() is not IUserMessage firstMessage) return null;
                    if (firstMessage.Author.Id != enlivenShardedClient.CurrentUser.Id) return null;
                    if (!firstMessage.Embeds.First().Title.Contains("Pack")) return null;
                    return firstMessage;
                });
            }
            private readonly IMessageChannel _channel;
            private readonly ConcurrentQueue<(DateTimeOffset date, string mention)> _entriesToAppend = new();
            private readonly SemaphoreSlim _semaphore = new(1);
            private readonly ILocalizationProvider _localizationProvider;
            private ulong? _lastMessageId;
            private Task<IUserMessage?> _currentMessageTask;

            public void OnNewMessageRecieved(ulong messageId) {
                _lastMessageId = messageId;
            }

            public void EnqueueEntry(DateTimeOffset dateTimeOffset, string channelMention) {
                _entriesToAppend.Enqueue((dateTimeOffset, channelMention));
            }
            
            public async Task<IUserMessage> ModifyOrSendMessage() {
                using var _ = await _semaphore.WaitDisposableAsync();
                var currentMessage = await GetCurrentMessageInternal();
                var newEntriesText = _entriesToAppend.DequeueExisting()
                    .Select(tuple => $"\n{tuple.date} in {tuple.mention}")
                    .JoinToString("");
                if (newEntriesText.IsBlank()) return currentMessage!;

                if (currentMessage != null) {
                    try {
                        return await UpdateMessageInternal(currentMessage, newEntriesText);
                    }
                    catch (Exception) {
                        _currentMessageTask = Task.FromResult<IUserMessage?>(null);
                    }
                }

                _currentMessageTask = SendMessageInternal(_channel, newEntriesText, _localizationProvider)!;
                return (await _currentMessageTask)!;
            }

            private async Task<IUserMessage?> GetCurrentMessageInternal() {
                var message = await _currentMessageTask;
                return _lastMessageId == null || _lastMessageId == message?.Id ? message : null;
            }

            private static async Task<IUserMessage> UpdateMessageInternal(IUserMessage message, string textToAppend) {
                var packBuilder = new EmbedBuilder()
                    .WithTitle("Deleted messages Pack")
                    .WithDescription(message.Embeds.First().Description);
                packBuilder.Description += textToAppend;
                await message.ModifyAsync(properties => properties.Embed = packBuilder.Build());
                return message;
            }

            private static async Task<IUserMessage> SendMessageInternal(IMessageChannel channel, string text, ILocalizationProvider loc) {
                var packBuilder = new EmbedBuilder()
                    .WithTitle("Deleted messages Pack")
                    .WithDescription(loc.Get("MessageHistory.DeletedMessagesPackDescription"));
                packBuilder.Description += text;
                return await channel.SendMessageAsync(embed: packBuilder.Build());
            }
        }
    }
}