using System;

namespace Bot.DiscordRelated.Interactions {
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
    sealed class SlashCommandOptionalAttribute : Attribute {
        public bool IsOptional { get; }
        public SlashCommandOptionalAttribute(bool isOptional = false) {
            IsOptional = isOptional;
        }
    }
}