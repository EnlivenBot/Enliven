using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Wrappers;
using Discord;

namespace Bot.DiscordRelated.Commands.Modules.Contexts;

public record InteractionsModuleContext(IEnlivenInteractionContext Context) : ICommonModuleContext {
    public IDiscordClient Client => Context.Client;
    public IGuild Guild => Context.Guild;
    public IMessageChannel Channel => Context.Channel;
    public IUser User => Context.User;

    public bool NeedResponse => Context.Interaction.NeedResponse;
    public bool CanSendEphemeral => true;

    public ValueTask BeforeExecuteAsync() {
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterExecuteAsync() {
        return ValueTask.CompletedTask;
    }

    public async Task<SentMessage> SendMessageAsync(string? text, Embed[]? embeds, bool ephemeral = false, MessageComponent? components = null) {
        // No loading and no responded
        if (Context.Interaction.NeedResponse) {
            await Context.Interaction.RespondAsync(text, embeds, ephemeral: ephemeral, components: components);
            return new SentMessage(() => Context.Interaction.GetOriginalResponseAsync(), ephemeral);
        }
        // Only loading sent
        if (Context.Interaction.CurrentResponseDeferred) {
            var message0 = await Context.Interaction.ModifyOriginalResponseAsync(properties => {
                properties.Content = text ?? Optional<string>.Unspecified;
                properties.Components = components ?? Optional<MessageComponent>.Unspecified;
                properties.Embeds = embeds ?? Optional<Embed[]>.Unspecified;
            });
            return new SentMessage(message0, false);
        }
        var message1 = await Context.Interaction.FollowupAsync(text, embeds, ephemeral: ephemeral, components: components);
        return new SentMessage(message1, ephemeral);
    }
}