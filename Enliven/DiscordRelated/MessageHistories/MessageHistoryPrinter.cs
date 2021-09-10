using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatExporter;
using ChatExporter.Exporter.MessageHistories;
using Common;
using Common.Entities;
using Common.Localization.Providers;
using Discord;
using HarmonyLib;

namespace Bot.DiscordRelated.MessageHistories {
    public class MessageHistoryPrinter {
        private MessageHistoryHtmlExporter _messageHistoryHtmlExporter;
        private HtmlRendererService _htmlRendererService;
        public MessageHistoryPrinter(MessageHistoryHtmlExporter messageHistoryHtmlExporter, HtmlRendererService htmlRendererService) {
            _messageHistoryHtmlExporter = messageHistoryHtmlExporter;
            _htmlRendererService = htmlRendererService;
        }

        public MessageHistoryPrinterData GenerateData(MessageHistory history, bool forceImage, ILocalizationProvider loc, IUser? requester, IUserMessage? existingMessage) {
            var embedBuilder = new EnlivenEmbedBuilder()
                .WithCurrentTimestamp()
                .WithTitle(loc.Get("MessageHistory.LogTitle"))
                .WithDescription(GenerateDescription(history, loc, existingMessage));

            if (requester != null)
                embedBuilder.WithRequester(requester, loc);

            if (!forceImage) {
                try {
                    var textBuilder = embedBuilder.Clone();
                    history
                        .GetSnapshots(loc)
                        .AsFields(loc)
                        .Select((builder, i) => builder.ToWrapper(int.MaxValue - i))
                        .Do(builder => textBuilder.AddField(null, builder));
                    return new MessageHistoryPrinterData(history, textBuilder.Build(), null);
                }
                catch (InvalidOperationException) {
                    // Embed too long, printing image
                }
            }

            var guild = new Optional<IGuild?>((existingMessage?.Channel as ITextChannel)?.Guild);
            var messageChannel = new Optional<ITextChannel?>(existingMessage?.Channel as ITextChannel);
            var existentMessage = new Optional<IMessage?>(existingMessage);
            var imageRenderTask = _messageHistoryHtmlExporter
                .ExportToHtml(history, true, true, messageChannel, existentMessage, guild)
                .Pipe(async task => await _htmlRendererService.RenderHtmlToStream(await task));

            return new MessageHistoryPrinterData(history, embedBuilder.Build(), imageRenderTask);
        }
        
        private static string GenerateDescription(MessageHistory history, ILocalizationProvider loc, IMessage? existingMessage) {
            var descriptionBuilder = new StringBuilder();
            if (existingMessage != null) descriptionBuilder.AppendLine(loc.Get("MessageHistory.ViewMessageExists", existingMessage.GetJumpUrl()));
            descriptionBuilder.AppendLine($"ID: {history.MessageId}");
            descriptionBuilder.AppendLine(loc.Get("MessageHistory.InChannel", history.ChannelId));
            descriptionBuilder.AppendLine(loc.Get("MessageHistory.ByAuthor", history.Author.Mention));
            if (history.HasAttachments) {
                descriptionBuilder.AppendLine(loc.Get("MessageHistory.AttachmentsTitle"));
                descriptionBuilder.AppendLine(MessageHistoryService.GetAttachmentString(history));
            }
            return descriptionBuilder.ToString();
        }

        public class MessageHistoryPrinterData {
            private readonly MessageHistory _history;
            private readonly Embed _embed;
            private readonly Task<MemoryStream>? _imageRenderTask;
            internal MessageHistoryPrinterData(MessageHistory history, Embed embed, Task<MemoryStream>? imageRenderTask) {
                _history = history;
                _embed = embed;
                _imageRenderTask = imageRenderTask;
            }

            public async Task<IUserMessage> SendAsync(IMessageChannel targetChannel) {
                if (_imageRenderTask != null) {
                    var filename = $"History-{_history.ChannelId}-{_history.MessageId}.png";
                    return await targetChannel.SendFileAsync(await _imageRenderTask, filename, embed: _embed);
                }
                else {
                    return await targetChannel.SendMessageAsync(embed: _embed);
                }
            }
        }
    }
}