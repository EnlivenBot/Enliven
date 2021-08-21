using System.Net;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord.Data.Common;

namespace ChatExporter.Exporter {
    public static class ExportUtilities {
        public static async Task<FileSize?> GetFileSizeFromUrlAsync(string url) {
            var req = WebRequest.Create(url);
            req.Method = "HEAD";
            using WebResponse resp = await req.GetResponseAsync();
            if (long.TryParse(resp.Headers.Get("Content-Length"), out var contentLength)) {
                return new FileSize(contentLength);
            }

            return null;
        }
    }
}