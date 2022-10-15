using System;
using System.Threading.Tasks;
using Discord;
using NLog;

namespace Bot.DiscordRelated.Music {
    public delegate Task<IUserMessage> SendControlMessageOverride(Embed embed, MessageComponent messageComponent);

    public static class SendControlMessageOverrideExtensions {
        public static async Task<IUserMessage> ExecuteAndFallbackWith(this SendControlMessageOverride? controlMessageOverride, Embed embed, MessageComponent messageComponent, IMessageChannel fallback, ILogger? logger = null) {
            if (controlMessageOverride != null) {
                try {
                    return await controlMessageOverride(embed, messageComponent);
                }
                catch (Exception e) {
                    logger?.Warn(e, "Failed to send control message via override");
                }
            }
            return await fallback.SendMessageAsync(null, false, embed, components: messageComponent);
        }
    }
}