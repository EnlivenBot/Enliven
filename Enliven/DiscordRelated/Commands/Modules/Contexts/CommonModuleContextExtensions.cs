using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Commands.Modules.Contexts;

public static class CommonModuleContextExtensions {
    public static Task<SentMessage> SendMessageAsync(this ICommonModuleContext advancedModuleBase, string? text, Embed? embed = null, bool ephemeral = false, MessageComponent? components = null) {
        return advancedModuleBase.SendMessageAsync(text, embed == null ? null : new[] { embed }, ephemeral, components);
    }
}