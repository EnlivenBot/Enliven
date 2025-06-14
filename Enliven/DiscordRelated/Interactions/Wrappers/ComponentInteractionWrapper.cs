using System;
using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class ComponentInteractionWrapper(IComponentInteraction interaction)
    : DiscordInteractionWrapperBase(interaction), IComponentInteraction
{
    public Task UpdateAsync(Action<MessageProperties> func, RequestOptions? options = null)
    {
        return interaction.UpdateAsync(func, options);
    }

    public Task DeferLoadingAsync(bool ephemeral = false, RequestOptions? options = null)
    {
        CurrentResponseDeferred = true;
        return interaction.DeferLoadingAsync(ephemeral, options);
    }

    public new IComponentInteractionData Data => interaction.Data;
    public IUserMessage Message => interaction.Message;
}