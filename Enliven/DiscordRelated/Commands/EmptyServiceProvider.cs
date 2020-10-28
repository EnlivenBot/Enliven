using System;

namespace Bot.DiscordRelated.Commands {
    public class EmptyServiceProvider : IServiceProvider {
        public static readonly EmptyServiceProvider Instance = new EmptyServiceProvider();

        public object? GetService(Type serviceType) => null;
    }
}