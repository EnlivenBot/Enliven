using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Config;
using Common.Localization.Providers;
using Discord;
using NLog;

namespace Common {
    public static class ExtensionMethods {
        public static void DelayedDelete(this IMessage message, TimeSpan span) {
            Task.Run(async () => {
                await Task.Delay(span);
                message.SafeDelete();
            });
        }

        public static void DelayedDelete(this Task<IUserMessage> message, TimeSpan span) {
            Task.Run(async () => {
                await Task.Delay(span);
                (await message.ConfigureAwait(false)).SafeDelete();
            });
        }

        public static void SafeDelete(this IMessage? message) {
            try {
                message?.DeleteAsync();
            }
            catch (Exception) {
                // ignored
            }
        }

        public static string Format(this string format, params object?[] args) {
            return string.Format(format, args);
        }

        public static string? SafeSubstring(this string? text, int start, int length) {
            if (text == null) return null;

            return text.Length <= start         ? ""
                : text.Length - start <= length ? text.Substring(start)
                                                  : text.Substring(start, length);
        }

        public static string? SafeSubstring(this string? text, int length, string postContent = "") {
            if (text == null) return null;

            return text.Length <= length ? text : text.Substring(0, length - postContent.Length) + postContent;
        }

        public static string Repeat(this string s, int count) {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (count <= 0) return string.Empty;
            var builder = new StringBuilder(s.Length * count);

            for (var i = 0; i < count; i++) builder.Append(s);

            return builder.ToString();
        }

        public static T Next<T>(this T src) where T : struct {
            if (!typeof(T).IsEnum) throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");

            var arr = (T[]) Enum.GetValues(src.GetType());
            var j = Array.IndexOf(arr, src) + 1;
            return arr.Length == j ? arr[0] : arr[j];
        }

        public static int Normalize(this int value, int min, int max) {
            return Math.Max(min, Math.Min(max, value));
        }

        // ReSharper disable once InconsistentNaming
        public static async Task<IMessage> SendTextAsFile(this IMessageChannel channel, string content, string filename, string? text = null,
                                                          bool isTTS = false,
                                                          Embed? embed = null, RequestOptions? options = null, bool isSpoiler = false) {
            await using var ms = new MemoryStream();
            TextWriter tw = new StreamWriter(ms);
            await tw.WriteAsync(content);
            await tw.FlushAsync();
            ms.Position = 0;
            return await channel.SendFileAsync(ms, filename);
        }

        public static void Log(this ILogger logger, LogSeverity logSeverity, Exception exception, string message, params object[] args) {
            var logLevel = logSeverity switch {
                LogSeverity.Critical => LogLevel.Fatal,
                LogSeverity.Error    => LogLevel.Error,
                LogSeverity.Warning  => LogLevel.Warn,
                LogSeverity.Info     => LogLevel.Info,
                LogSeverity.Verbose  => LogLevel.Debug,
                LogSeverity.Debug    => LogLevel.Trace,
                _                    => throw new ArgumentOutOfRangeException()
            };
            logger.Log(logLevel, exception, message, args);
        }

        public static TResult Try<TSource, TResult>(this TSource o, Func<TSource, TResult> action, Func<TSource, TResult> onFail) {
            try {
                return action(o);
            }
            catch {
                return onFail(o);
            }
        }

        public static TResult Try<TSource, TResult>(this TSource o, Func<TSource, TResult> action, TResult onFail) {
            try {
                return action(o);
            }
            catch {
                return onFail;
            }
        }

        public static LocalizationContainer ToContainer(this ILocalizationProvider provider) {
            if (provider is LocalizationContainer localizationContainer) return localizationContainer;
            return new LocalizationContainer(provider);
        }

        /// <summary>
        /// Extension method for fast string validation. WARN: Actually the IsNullOrWhiteSpace method is implied
        /// </summary>
        public static bool IsBlank(this string? source) {
            return string.IsNullOrWhiteSpace(source);
        }

        /// <summary>
        /// Extension method for fast string getting. WARN: Actually the IsNullOrWhiteSpace method is implied
        /// </summary>
        /// <param name="source">Source string</param>
        /// <param name="replacement">Replacement</param>
        /// <returns>If target string is null or whitespace - return <paramref name="replacement"/>. Otherwise - return <paramref name="source"/></returns>
        public static string IsBlank(this string? source, string replacement) {
            return string.IsNullOrWhiteSpace(source) ? replacement : source;
        }

        public static string FormattedToString(this TimeSpan span) {
            string s = $"{span:mm':'ss}";
            if ((int) span.TotalHours != 0)
                s = s.Insert(0, $"{(int) span.TotalHours}:");
            return s;
        }

        public static UserLink ToLink(this IUser user) {
            return new UserLink(user.Id);
        }
        
        public static TimeSpan Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, TimeSpan> func)
        {
            return new TimeSpan(source.Sum(item => func(item).Ticks));
        }
    }
}