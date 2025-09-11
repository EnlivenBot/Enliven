using System;
using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class ComponentInteractionWrapper(IComponentInteraction interaction)
    : DiscordInteractionWrapperBase(interaction), IComponentInteraction {
    private bool _deferedWithoutLoading;

    public async Task UpdateAsync(Action<MessageProperties> func, RequestOptions? options = null) {
        SetRespondStarted();
        if (_deferedWithoutLoading) {
            await interaction.ModifyOriginalResponseAsync(func, options);
        }
        else {
            await interaction.UpdateAsync(func, options);
        }
    }

    public async Task DeferLoadingAsync(bool ephemeral = false, RequestOptions? options = null) {
        SetRespondStarted();
        CurrentResponseDeferred = true;
        await interaction.DeferLoadingAsync(ephemeral, options);
    }

    public override async Task DeferAsync(bool ephemeral = false, RequestOptions? options = null) {
        SetRespondStarted();
        _deferedWithoutLoading = true;
        await Interaction.DeferAsync(ephemeral, options);
    }

    public new IComponentInteractionData Data => interaction.Data;
    public IUserMessage Message => interaction.Message;
}