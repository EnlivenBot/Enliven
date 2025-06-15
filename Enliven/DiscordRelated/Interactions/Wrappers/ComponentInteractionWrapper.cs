using System;
using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class ComponentInteractionWrapper(IComponentInteraction interaction)
    : DiscordInteractionWrapperBase(interaction), IComponentInteraction
{
    public async Task UpdateAsync(Action<MessageProperties> func, RequestOptions? options = null)
    {
        SetRespondStarted();
        await interaction.UpdateAsync(func, options);
    }

    public async Task DeferLoadingAsync(bool ephemeral = false, RequestOptions? options = null)
    {
        SetRespondStarted();
        CurrentResponseDeferred = true;
        await interaction.DeferLoadingAsync(ephemeral, options);
    }

    public new IComponentInteractionData Data => interaction.Data;
    public IUserMessage Message => interaction.Message;
}