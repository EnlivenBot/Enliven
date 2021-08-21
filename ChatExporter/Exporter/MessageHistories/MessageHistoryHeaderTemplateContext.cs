using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data.Common;
using DiscordChatExporter.Core.Utils;
using Attachment = DiscordChatExporter.Core.Discord.Data.Attachment;

namespace ChatExporter.Exporter.MessageHistories {
    internal class MessageHistoryHeaderTemplateContext {
        public ITextChannel? Channel { get; }
        public IMessage? Message { get; }
        public MessageHistoryExportContext Context { get; }

        public MessageHistoryHeaderTemplateContext(MessageHistoryExportContext context, ITextChannel? channel, IMessage? message) {
            Context = context;
            Channel = channel;
            Message = message;
            _attachmentsLazy = new Lazy<Task<List<Attachment>>>(ResolveAttachmentsInternalAsync);
        }

        public DateTimeOffset? GetCreationTime() {
            return Context.MessageHistory.IsHistoryUnavailable
                ? Message?.CreatedAt
                : Context.MessageHistory.GetSnapshots(null!).FirstOrDefault()?.EditTimestamp;
        }

        private readonly Lazy<Task<List<Attachment>>> _attachmentsLazy;
        public Task<List<Attachment>> GetAttachmentsAsync() {
            return _attachmentsLazy.Value;
        }
        
        private async Task<List<Attachment>> ResolveAttachmentsInternalAsync() {
            var messageHistoryAttachments = Context.MessageHistory.Attachments ?? new List<string>();
            var creatingTasks = messageHistoryAttachments
                .Select(s => CreateAttachmentFromUrl(s, Context.WillBeRenderedToImage));
            
            return (await Task.WhenAll(creatingTasks)).ToList();
        }
        
        private static readonly Regex AttachmentParseRegex = new Regex(@"\/(\d+)\/([^\/]+\.\S+)");
        public static async Task<Attachment> CreateAttachmentFromUrl(string url, bool fetchSizeFromMedia) {
            var match = AttachmentParseRegex.Match(url);
            var id = ulong.Parse(match.Groups[1].Value);
            var name = match.Groups[2].Value;
            var format = Path.GetExtension(name);

            var size = new FileSize(0);
            if (!FileFormat.IsImage(format)
             && (!FileFormat.IsAudio(format) || fetchSizeFromMedia)
             && (!FileFormat.IsVideo(format) || fetchSizeFromMedia))
                size = await ExportUtilities.GetFileSizeFromUrlAsync(url) ?? new FileSize(0);

            return new Attachment(new Snowflake(id), url, name, null, null, size);
        }
    }
}