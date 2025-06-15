using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class AutocompleteInteractionWrapper(IAutocompleteInteraction interaction)
    : DiscordInteractionWrapperBase(interaction), IAutocompleteInteraction
{
    public async Task RespondAsync(IEnumerable<AutocompleteResult> result, RequestOptions? options = null)
    {
        SetRespondStarted();
        await interaction.RespondAsync(result, options);
    }

    public new IAutocompleteInteractionData Data => interaction.Data;
}