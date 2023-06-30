using Common.Entities;

namespace ChatExporter.Exporter.MessageHistories {
    internal class MessageHistorySnapshotContext {
        internal MessageHistorySnapshotContext(MessageHistoryExportContext context, MessageSnapshot snapshot) {
            Context = context;
            Snapshot = snapshot;
        }
        public MessageHistoryExportContext Context { get; }
        public MessageSnapshot Snapshot { get; }

        public string FormatMarkdown(MessageSnapshot snapshot) =>
            MessageHistorySnapshotVisitor.FormatMessageSnapshot(Context, snapshot);
    }
}