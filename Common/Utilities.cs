using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using NLog;

namespace Common {
    public static class Utilities {
        public static IEnumerable<string> SplitToLines(string stringToSplit, int maximumLineLength) {
            return Regex.Matches(stringToSplit, @"(.{1," + maximumLineLength + @"})(?:\s|$)").Select(match => match.Value);
        }
        
        public static async Task<TResult> TryAsync<TResult>(Func<Task<TResult>> action, Func<Exception, TResult> onFail) {
            try {
                return await action();
            }
            catch (Exception e) {
                return onFail(e);
            }
        }
        
        public static TResult Try<TResult>(Func<TResult> action, Func<Exception, TResult> onFail) {
            try {
                return action();
            }
            catch (Exception e) {
                return onFail(e);
            }
        }
        
        public static TResult Try<TResult>(Func<TResult> action, TResult onFail) {
            try {
                return action();
            }
            catch (Exception) {
                return onFail;
            }
        }

        public static bool IsValidUrl(string query) {
            return Uri.TryCreate(query, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeFile || uriResult.Scheme == Uri.UriSchemeFtp ||
                    uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps ||
                    uriResult.Scheme == Uri.UriSchemeNetTcp);
        }
        
        public static Task OnDiscordLog(ILogger logger, LogMessage message) {
            if (message.Message != null && message.Message.StartsWith("Unknown Dispatch")) {
                return Task.CompletedTask;
            }

            logger.LogDiscord(message.Severity, message.Exception, "{message} from {source}", message.Message!, message.Source);
            return Task.CompletedTask;
        }
    }
}