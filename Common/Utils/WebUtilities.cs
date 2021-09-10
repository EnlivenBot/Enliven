using System.Net;
using System.Threading.Tasks;

namespace Common.Utils {
    public class WebUtilities {
        public static async Task<long?> GetFileSizeFromUrlAsync(string url) {
            var req = WebRequest.Create(url);
            req.Method = "HEAD";
            using WebResponse resp = await req.GetResponseAsync();
            if (long.TryParse(resp.Headers.Get("Content-Length"), out var contentLength)) {
                return contentLength;
            }

            return null;
        }
    }
}