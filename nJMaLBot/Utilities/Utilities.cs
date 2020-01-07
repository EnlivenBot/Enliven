using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using Bot.Config;
using Discord;
using Newtonsoft.Json;

namespace Bot.Utilities
{
    class Utilities
    {
        public static string DownloadFile(string url, string path) {
            using (WebClient wc = new WebClient()) {
                wc.DownloadFile(url, path);
            }

            return path;
        }

        public static string DownloadString(string url) {
            using WebClient wc = new WebClient();
            return wc.DownloadString(url);
        }
    }
}
