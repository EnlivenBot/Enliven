using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Wrappers;
using Discord;

namespace Bot.DiscordRelated.Commands.Modules.Contexts;

public static class CommonModuleContextExtensions {
    public static Task<SentMessage> SendMessageAsync(this ICommonModuleContext advancedModuleBase, string? text,
        Embed? embed = null, bool ephemeral = false, MessageComponent? components = null) {
        return advancedModuleBase.SendMessageAsync(text, embed == null ? null : [embed], ephemeral, components);
    }

    public static bool InteractionBasedResponseRequired(this ICommonModuleContext context,
        [NotNullWhen(true)] out IEnlivenInteraction? interaction) {
        interaction = null;
        if (context is InteractionsModuleContext { NeedResponse: true } interactionContext) {
            interaction = interactionContext.Context.Interaction;
            return true;
        }

        return false;
    }
}