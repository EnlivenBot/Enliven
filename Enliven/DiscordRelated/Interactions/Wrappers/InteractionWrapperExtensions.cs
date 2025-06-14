using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public static class InteractionWrapperExtensions {
    public static async Task RemoveDeferredMessageIfNeeded(this IEnlivenInteraction enlivenInteraction) {
        if (enlivenInteraction.CurrentResponseDeferred) {
            var restInteractionMessage = await enlivenInteraction.GetOriginalResponseAsync();
            if (restInteractionMessage is not null && (restInteractionMessage.Flags & MessageFlags.Loading) != 0)
                await restInteractionMessage.DeleteAsync();
        }
    }
}