using System;
using System.Threading.Tasks;
using Discord;

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

        public static string Format(this string format, object arg) {
            return string.Format(format, arg);
        }
        
        public static string Format(this string format, object arg1, object arg2) {
            return string.Format(format, arg1, arg2);
        }
    }
}