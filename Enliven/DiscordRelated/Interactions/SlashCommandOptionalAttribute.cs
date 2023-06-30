using System;

namespace Bot.DiscordRelated.Interactions;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
internal sealed class SlashCommandOptionalAttribute : Attribute {
    public SlashCommandOptionalAttribute(bool isOptional = false) {
        IsOptional = isOptional;
    }
    public bool IsOptional { get; }
}