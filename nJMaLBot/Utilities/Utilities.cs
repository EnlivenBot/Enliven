using System.Net;

namespace Bot.Utilities {
    internal static class Utilities {
        public static string DownloadFile(string url, string path) {
            using var wc = new WebClient();
            wc.DownloadFile(url, path);

            return path;
        }

        public static string DownloadString(string url) {
            using var wc = new WebClient();
            return wc.DownloadString(url);
        }
    }
}