using System;

namespace Bot.DiscordRelated.Commands.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
internal sealed class ShouldCreatePlayerAttribute : Attribute { }