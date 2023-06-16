using Common.Entities;
using DiscordChatExporter.Core.Exporting.Writers.MarkdownVisitors;

namespace ChatExporter.Exporter.MessageHistories
{
    internal class MessageHistorySnapshotContext {
        public MessageHistoryExportContext Context { get; }
        public MessageSnapshot Snapshot { get; }
        internal MessageHistorySnapshotContext(MessageHistoryExportContext context, MessageSnapshot snapshot) {
            Context = context;
            Snapshot = snapshot;
        }

        public string FormatMarkdown(MessageSnapshot snapshot) =>
            MessageHistorySnapshotVisitor.FormatMessageSnapshot(Context, snapshot);
    }
}