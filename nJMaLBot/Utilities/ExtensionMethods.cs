using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Utilities.Commands;
using Discord;
using Discord.Commands;

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
            if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));

            T[] Arr = (T[]) Enum.GetValues(src.GetType());
            int j = Array.IndexOf<T>(Arr, src) + 1;
            return (Arr.Length == j) ? Arr[0] : Arr[j];
        }

        public static EmbedBuilder GetAuthorEmbedBuilder(this ModuleBase moduleBase) {
            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithFooter(moduleBase.Context.User.Username, moduleBase.Context.User.GetAvatarUrl());
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
    }
}