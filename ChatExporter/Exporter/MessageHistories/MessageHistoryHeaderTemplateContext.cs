using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Utils;
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

        public static DiscordHelper.NeedFetchSize FetchForAllExceptImages = format => !FileFormat.IsImage(format);
        public static DiscordHelper.NeedFetchSize FetchForAllExceptMedia = format => !FileFormat.IsImage(format) && !FileFormat.IsAudio(format) && !FileFormat.IsVideo(format);
        
        public static async Task<Attachment> CreateAttachmentFromUrl(string url, bool fetchSizeFromMedia) {
            var needFetchPredicate = fetchSizeFromMedia ? FetchForAllExceptImages : FetchForAllExceptMedia;
            return (await DiscordHelper.ParseAttachmentFromUrlAsync(url, needFetchPredicate)).ToAttachment();
        }
    }
}