using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Wrappers;
using Discord;

namespace Bot.DiscordRelated.Commands.Modules.Contexts;

public class InteractionsModuleContext(IEnlivenInteractionContext context)
    : ICommonModuleContext {

    public IDiscordClient Client => context.Client;
    public IGuild Guild => context.Guild;
    public IMessageChannel Channel => context.Channel;
    public IUser User => context.User;

    public bool NeedResponse => context.Interaction.NeedResponse;
    public bool CanSendEphemeral => true;

    public ValueTask BeforeExecuteAsync() {
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterExecuteAsync() {
        return ValueTask.CompletedTask;
    }

    public async Task<SentMessage> SendMessageAsync(string? text, Embed[]? embeds, bool ephemeral = false, MessageComponent? components = null) {
        // No loading and no responded
        if (context.Interaction.NeedResponse) {
            await context.Interaction.RespondAsync(text, embeds, ephemeral: ephemeral, components: components);
            return new SentMessage(() => context.Interaction.GetOriginalResponseAsync(), ephemeral);
        }
        // Only loading sent
        if (context.Interaction.CurrentResponseDeferred) {
            var message0 = await context.Interaction.ModifyOriginalResponseAsync(properties => {
                properties.Content = text ?? Optional<string>.Unspecified;
                properties.Components = components ?? Optional<MessageComponent>.Unspecified;
                properties.Embeds = embeds ?? Optional<Embed[]>.Unspecified;
            });
            return new SentMessage(message0, false);
        }
        var message1 = await context.Interaction.FollowupAsync(text, embeds, ephemeral: ephemeral, components: components);
        return new SentMessage(message1, ephemeral);
    }
}