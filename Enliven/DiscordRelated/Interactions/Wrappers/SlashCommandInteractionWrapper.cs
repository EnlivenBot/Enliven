using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class SlashCommandInteractionWrapper(ISlashCommandInteraction interaction)
    : DiscordInteractionWrapperBase(interaction), ISlashCommandInteraction
{
    IApplicationCommandInteractionData IApplicationCommandInteraction.Data => interaction.Data;

    IApplicationCommandInteractionData ISlashCommandInteraction.Data => interaction.Data;
}