using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class AutocompleteInteractionWrapper(IAutocompleteInteraction interaction)
    : DiscordInteractionWrapperBase(interaction), IAutocompleteInteraction
{
    public Task RespondAsync(IEnumerable<AutocompleteResult> result, RequestOptions? options = null)
    {
        return interaction.RespondAsync(result, options);
    }

    public new IAutocompleteInteractionData Data => interaction.Data;
}