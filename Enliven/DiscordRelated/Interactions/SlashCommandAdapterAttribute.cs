using System;

namespace Bot.DiscordRelated.Interactions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class SlashCommandAdapterAttribute : Attribute {
    public SlashCommandAdapterAttribute(bool processSlashCommand = true) {
        ProcessSlashCommand = processSlashCommand;
    }
    public bool ProcessSlashCommand { get; }
}