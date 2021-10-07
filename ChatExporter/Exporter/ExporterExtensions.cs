using System;

namespace ChatExporter.Exporter {
    public static class ExporterExtensions {
        public static string Format(this DateTimeOffset offset) {
            return offset.ToUniversalTime().ToString("dd-MMM-yy HH:mm");
        }
    }
}