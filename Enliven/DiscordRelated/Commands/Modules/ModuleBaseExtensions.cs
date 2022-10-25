using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules.Contexts;
using Common;
using Common.Localization.Entries;
using Discord;

namespace Bot.DiscordRelated.Commands.Modules {
    public static class ModuleBaseExtensions {
        public static Task<SentMessage> ReplySuccessFormattedAsync(this AdvancedModuleBase advancedModuleBase, IEntry description, bool tryToSendEphemeral = false)
            => advancedModuleBase.ReplyFormattedAsync(EntryLocalized.Success, description, tryToSendEphemeral, Color.Gold);

        public static Task<SentMessage> ReplyFailFormattedAsync(this AdvancedModuleBase advancedModuleBase, IEntry description, bool tryToSendEphemeral = false)
            => advancedModuleBase.ReplyFormattedAsync(EntryLocalized.Fail, description, tryToSendEphemeral, Color.Orange);

        public static Task<SentMessage> ReplyFormattedAsync(this AdvancedModuleBase advancedModuleBase, IEntry title, IEntry description, bool tryToSendEphemeral = false, Color? embedColor = null) {
            var embed = new EmbedBuilder()
                .WithTitle(advancedModuleBase.Loc.Resolve(title))
                .WithDescription(advancedModuleBase.Loc.Resolve(description))
                .Pipe(builder => embedColor != null ? builder.WithColor(embedColor.Value) : builder)
                .Build();
            return advancedModuleBase.Context.SendMessageAsync(null, embed, tryToSendEphemeral);
        }

        public static ValueTask RemoveMessageInvokerIfPossible(this AdvancedModuleBase advancedModuleBase) {
            if (advancedModuleBase.Context is TextCommandsModuleContext textContext) {
                return textContext.Message.SafeDeleteAsync();
            }

            return ValueTask.CompletedTask;
        }
    }
}