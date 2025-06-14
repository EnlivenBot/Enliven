using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class UserCommandInteractionWrapper(IUserCommandInteraction interaction)
    : DiscordInteractionWrapperBase(interaction), IUserCommandInteraction
{
    IApplicationCommandInteractionData IApplicationCommandInteraction.Data => interaction.Data;

    public new IUserCommandInteractionData Data => interaction.Data;
}