using System;
using System.Linq;
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

        public static string Format(this string format, object arg) {
            return string.Format(format, arg);
        }

        public static string Format(this string format, object arg1, object arg2) {
            return string.Format(format, arg1, arg2);
        }

        public static string SafeSubstring(this string text, int start, int length) {
            if (text == null) return null;

            return text.Length <= start         ? ""
                : text.Length - start <= length ? text.Substring(start)
                                                  : text.Substring(start, length);
        }
    }
}