using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Config.Localization.Providers;
using Bot.Utilities.Commands;
using Bot.Utilities.Modules;
using Discord;
using Discord.Commands;
using NLog;

namespace Bot.Utilities {
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
                (await message).SafeDelete();
            });
        }

        public static void SafeDelete(this IMessage message) {
            try {
                message.DeleteAsync();
            }
            catch (Exception) {
                // ignored
            }
        }

        public static GroupingAttribute GetGroup(this CommandInfo info) {
            return (info.Attributes.FirstOrDefault(attribute => attribute is GroupingAttribute) ??
                    info.Module.Attributes.FirstOrDefault(attribute => attribute is GroupingAttribute)) as GroupingAttribute;
        }
        
        public static string GetLocalizedName(this GroupingAttribute groupingAttribute, ILocalizationProvider loc) {
            return loc.Get($"Groups.{groupingAttribute?.GroupName ?? ""}");
        }

        public static bool IsHiddenCommand(this CommandInfo info) {
            return (info.Attributes.FirstOrDefault(attribute => attribute is HiddenAttribute) ??
                    info.Module.Attributes.FirstOrDefault(attribute => attribute is HiddenAttribute)) != null;
        }

        public static string Format(this string format, params object?[] args) {
            return string.Format(format, args);
        }

        public static string SafeSubstring(this string text, int start, int length) {
            if (text == null) return null;

            return text.Length <= start         ? ""
                : text.Length - start <= length ? text.Substring(start)
                                                  : text.Substring(start, length);
        }

        public static string SafeSubstring(this string text, int length, string postContent = "") {
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

        public static EmbedBuilder GetAuthorEmbedBuilder(this AdvancedModuleBase moduleBase) {
            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithFooter(moduleBase.Loc.Get("Commands.RequestedBy").Format(moduleBase.Context.User.Username),
                moduleBase.Context.User.GetAvatarUrl());
            return embedBuilder;
        }

        public static int Normalize(this int value, int min, int max) {
            return Math.Max(min, Math.Min(max, value));
        }

        public static async Task<IMessage> SendTextAsFile(this IMessageChannel channel, string content, string filename, string text = null, bool isTTS = false,
                                                          Embed embed = null, RequestOptions options = null, bool isSpoiler = false) {
            await using var ms = new MemoryStream();
            TextWriter tw = new StreamWriter(ms);
            await tw.WriteAsync(content);
            await tw.FlushAsync();
            ms.Position = 0;
            return await channel.SendFileAsync(ms, filename);
        }

        public static void Log(this Logger logger, LogSeverity logSeverity, Exception exception, string message, params object[] args) {
            var logLevel = logSeverity switch {
                LogSeverity.Critical => LogLevel.Fatal,
                LogSeverity.Error    => LogLevel.Error,
                LogSeverity.Warning  => LogLevel.Warn,
                LogSeverity.Info     => LogLevel.Info,
                LogSeverity.Verbose  => LogLevel.Debug,
                LogSeverity.Debug    => LogLevel.Trace,
                _                    => throw new ArgumentOutOfRangeException()
            };
            logger.Log(logLevel,exception, message, args);
        }
    }
}