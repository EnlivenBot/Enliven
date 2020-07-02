using System;

namespace Bot.Utilities.Commands {
    public class EmptyServiceProvider : IServiceProvider {
        public static readonly EmptyServiceProvider Instance = new EmptyServiceProvider();

        public object? GetService(Type serviceType) => null;
    }
}