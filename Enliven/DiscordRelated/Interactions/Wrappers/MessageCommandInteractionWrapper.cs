using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class MessageCommandInteractionWrapper(IMessageCommandInteraction interaction)
    : DiscordInteractionWrapperBase(interaction), IMessageCommandInteraction
{
    IApplicationCommandInteractionData IApplicationCommandInteraction.Data => interaction.Data;

    public new IMessageCommandInteractionData Data => interaction.Data;
}