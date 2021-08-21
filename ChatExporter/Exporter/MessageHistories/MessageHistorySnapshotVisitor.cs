using System;
using System.Linq;
using System.Net;
using System.Text;
using Common.Entities;
using DiffMatchPatch;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Exporting.Writers.MarkdownVisitors;
using DiscordChatExporter.Core.Markdown;
using DiscordChatExporter.Core.Markdown.Parsing;
using DiscordChatExporter.Core.Utils.Extensions;

namespace ChatExporter.Exporter.MessageHistories {
    internal class MessageHistorySnapshotVisitor : HtmlMarkdownVisitor {
        private MessageHistoryExportContext _exportContext;
        private StringBuilder _buffer;
        public MessageHistorySnapshotVisitor(MessageHistoryExportContext context, StringBuilder buffer, bool isJumbo) : base(null!, buffer, isJumbo) {
            _buffer = buffer;
            _exportContext = context;
        }

        protected override MarkdownNode VisitMention(MentionNode mention) {
            var mentionId = Snowflake.TryParse(mention.Id);
            if (mention.Kind == MentionKind.Meta)
            {
                _buffer
                    .Append("<span class=\"mention\">")
                    .Append("@").Append(HtmlEncode(mention.Id))
                    .Append("</span>");
            }
            else if (mention.Kind == MentionKind.User)
            {
                var member = mentionId?.Pipe(_exportContext.TryGetMember);
                var fullName = member?.User.FullName ?? $"Unknown (ID: {mentionId})";
                var nick = member?.Nick ?? "Unknown ({mentionId})";

                _buffer
                    .Append($"<span class=\"mention\" title=\"{HtmlEncode(fullName)}\">")
                    .Append("@").Append(HtmlEncode(nick))
                    .Append("</span>");
            }
            else if (mention.Kind == MentionKind.Channel)
            {
                var channel = mentionId?.Pipe(_exportContext.TryGetChannel);
                var symbol = channel?.IsVoiceChannel == true ? "🔊" : "#";
                var name = channel?.Name ?? $"deleted-channel ({mentionId})";

                _buffer
                    .Append("<span class=\"mention\">")
                    .Append(symbol).Append(HtmlEncode(name))
                    .Append("</span>");
            }
            else if (mention.Kind == MentionKind.Role)
            {
                var role = mentionId?.Pipe(_exportContext.TryGetRole);
                var name = role?.Name ?? $"deleted-role ({mentionId})";
                var color = role?.Color;

                var style = color.HasValue
                    ? $"color: rgb({color?.R}, {color?.G}, {color?.B}); background-color: rgba({color?.R}, {color?.G}, {color?.B}, 0.1);"
                    : "";

                _buffer
                    .Append($"<span class=\"mention\" style=\"{style}\">")
                    .Append("@").Append(HtmlEncode(name))
                    .Append("</span>");
            }

            return mention;
        }
        
        protected override MarkdownNode VisitUnixTimestamp(UnixTimestampNode timestamp) {
            // Timestamp tooltips always use full date regardless of the configured format
            var longDateString = timestamp.Value.ToLocalString("dddd, MMMM d, yyyy h:mm tt");

            _buffer
                .Append($"<span class=\"timestamp\" title=\"{HtmlEncode(longDateString)}\">")
                .Append(HtmlEncode(timestamp.Value.Format()))
                .Append("</span>");

            return timestamp;
        }
        
        private static string HtmlEncode(string text) => WebUtility.HtmlEncode(text);

        public static string FormatMessageSnapshot(MessageHistoryExportContext context, MessageSnapshot snapshot)
        {
            // Cases like this should be filtered out before calling this method
            if (snapshot.IsAboutHistoryUnavailability) 
                throw new ArgumentException("History unavailable snapshots must be filtered before calling this method");
            var diffs = snapshot.GetEdits()!.ToList();
            if (diffs.Count == 0) {
                return @"<i>Empty message</i>";
            }
            
            var buffer = new StringBuilder();
            foreach (var diff in diffs) {
                var (tagOpen, tagClose) = diff.Operation switch {
                    Operation.Insert => ("<div style=\"background:DarkGreen;\">", "</div>"),
                    Operation.Delete => ("<div style=\"background:DarkRed;\">", "</div>"),
                    _                => (null, null)
                };
                buffer.Append(tagOpen);
                
                var nodes = MarkdownParser.Parse(diff.Text);
                new MessageHistorySnapshotVisitor(context, buffer, false).Visit(nodes);
                
                buffer.Append(tagClose);
            }
            
            return buffer.ToString();
        }
    }
}