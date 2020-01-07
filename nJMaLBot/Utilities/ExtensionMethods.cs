using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

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
    }
}