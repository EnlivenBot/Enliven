using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

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

        public static IEnumerable<string> SplitToLines(string stringToSplit, int maximumLineLength) {
            return Regex.Matches(stringToSplit, @"(.{1," + maximumLineLength + @"})(?:\s|$)").Select(match => match.Value);
        }
        
        public static TResult Try<TResult>(Func<TResult> action, Func<TResult> onFail) {
            try {
                return action();
            }
            catch {
                return onFail();
            }
        }
        
        public static TResult Try<TResult>(Func<TResult> action, TResult onFail) {
            try {
                return action();
            }
            catch {
                return onFail;
            }
        }
    }
}