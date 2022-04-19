using System;

namespace Bot.DiscordRelated.Commands.Modules {
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class ShouldCreatePlayerAttribute : Attribute { }
}