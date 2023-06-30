using System;

namespace Bot.DiscordRelated.Commands.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class LongRunningCommandAttribute : Attribute {
    public LongRunningCommandAttribute(bool isLongRunning = true) {
        IsLongRunning = isLongRunning;
    }
    public bool IsLongRunning { get; }
}