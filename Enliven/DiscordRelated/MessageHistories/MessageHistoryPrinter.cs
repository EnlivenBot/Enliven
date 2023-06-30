using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatExporter;
using ChatExporter.Exporter.MessageHistories;
using Common;
using Common.Config;
using Common.Entities;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Discord;

namespace Bot.DiscordRelated.MessageHistories;

public class MessageHistoryPrinter {
    private readonly HtmlRendererService _htmlRendererService;
    private readonly MessageHistoryHtmlExporter _messageHistoryHtmlExporter;
    public MessageHistoryPrinter(MessageHistoryHtmlExporter messageHistoryHtmlExporter, HtmlRendererService htmlRendererService) {
        _messageHistoryHtmlExporter = messageHistoryHtmlExporter;
        _htmlRendererService = htmlRendererService;
    }

    public IMessageSendData GenerateDataForDeleted(MessageHistory history, ILocalizationProvider loc, MessageExportType messageExportType) {
        var title = new EntryLocalized("MessageHistory.MessageWasDeleted");
        var description = new EntryString(GenerateDescription(history, loc, null));
        var messageHistoryPrinterData = GenerateDataInternal(history, title, description, false, messageExportType, loc, null, null);
        if (messageHistoryPrinterData.ImageRenderTask != null) messageHistoryPrinterData.Text = "===========================================";
        return messageHistoryPrinterData;
    }

    public IMessageSendData GenerateDataForDeletedWithoutHistory(ITextChannel deletedInChannel, ulong messageId, ILocalizationProvider loc) {
        var embedBuilder = new EnlivenEmbedBuilder()
            .WithCurrentTimestamp()
            .WithTitle(loc.Get("MessageHistory.MessageWasDeleted"))
            .WithDescription($"ID: {messageId}\n"
                           + loc.Get("MessageHistory.InChannel", deletedInChannel.Id))
            .AddField(null, loc.Get("MessageHistory.LastContent"), loc.Get("MessageHistory.Unavailable"));
        return new MessageHistoryPrinterData(embedBuilder.Build());
    }

    public IMessageSendData GenerateDataForLog(MessageHistory? history, bool forceImage,
                                               ILocalizationProvider loc, IUser? requester, IUserMessage? existingMessage) {
        if (history != null) {
            var title = new EntryLocalized("MessageHistory.LogTitle");
            var description = new EntryString(GenerateDescription(history, loc, existingMessage));
            return GenerateDataInternal(history, title, description, forceImage, MessageExportType.Image, loc, requester, existingMessage);
        }
        return GenerateDataWithoutHistoryInternal(loc, requester, existingMessage);
    }

    private MessageHistoryPrinterData GenerateDataWithoutHistoryInternal(ILocalizationProvider loc, IUser? requester, IUserMessage? existingMessage) {
        var descriptionBuilder = new StringBuilder();
        if (existingMessage == null)
            descriptionBuilder.AppendLine(loc.Get("MessageHistory.MessageNull"));
        else {
            descriptionBuilder.AppendLine(loc.Get("MessageHistory.ViewMessageExists", existingMessage.GetJumpUrl()));
            descriptionBuilder.AppendLine($"ID: {existingMessage.Id}");
            descriptionBuilder.AppendLine(loc.Get("MessageHistory.InChannel", existingMessage.Channel.Id));
            descriptionBuilder.AppendLine(loc.Get("MessageHistory.ByAuthor", existingMessage.Author.Mention));
        }
        var title = new EntryLocalized("MessageHistory.LogTitle");

        var embedBuilder = new EnlivenEmbedBuilder()
            .WithCurrentTimestamp()
            .WithTitle(title.Get(loc))
            .WithDescription(descriptionBuilder.ToString());

        if (requester != null)
            embedBuilder.WithRequester(requester, loc);

        return new MessageHistoryPrinterData(embedBuilder.Build());
    }

    private MessageHistoryPrinterData GenerateDataInternal(MessageHistory history, IEntry title, IEntry description,
                                                           bool forceImage, MessageExportType messageExportType,
                                                           ILocalizationProvider loc, IUser? requester, IUserMessage? existingMessage) {
        var embedBuilder = new EnlivenEmbedBuilder()
            .WithCurrentTimestamp()
            .WithTitle(title.Get(loc))
            .WithDescription(description.Get(loc));

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
                return new MessageHistoryPrinterData(textBuilder.Build());
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
            .Pipe(async task => {
                return messageExportType switch {
                    MessageExportType.Image => await _htmlRendererService.RenderHtmlToStream(await task),
                    MessageExportType.Html  => CreateStreamFromString(await task),
                    _                       => throw new ArgumentOutOfRangeException(nameof(messageExportType), messageExportType, null)
                };
            });

        var fileExtension = messageExportType switch {
            MessageExportType.Image => "png",
            MessageExportType.Html  => "html",
            _                       => throw new ArgumentOutOfRangeException(nameof(messageExportType), messageExportType, null)
        };

        return new MessageHistoryPrinterData(embedBuilder.Build(), imageRenderTask, $"History-{history.ChannelId}-{history.MessageId}.{fileExtension}");
    }

    public static Stream CreateStreamFromString(string str) {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(str);
        writer.Flush();
        stream.Position = 0;
        return stream;
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

    private class MessageHistoryPrinterData : IMessageSendData {
        internal MessageHistoryPrinterData(Embed embed) {
            Embed = embed;
        }

        internal MessageHistoryPrinterData(Embed embed, Task<Stream> imageRenderTask, string filename) {
            Filename = filename;
            Embed = embed;
            ImageRenderTask = imageRenderTask;
        }
        internal Embed Embed { get; }
        internal Task<Stream>? ImageRenderTask { get; }
        internal string? Filename { get; }
        internal string? Text { get; set; }

        public async Task<IUserMessage> SendMessage(IMessageChannel targetChannel) {
            if (ImageRenderTask != null)
                return await targetChannel.SendFileAsync(await ImageRenderTask, Filename, Text, embed: Embed);
            else
                return await targetChannel.SendMessageAsync(Text, embed: Embed);
        }
    }
}