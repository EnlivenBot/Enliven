using System;
using System.Runtime.Serialization;
using Common.Localization.Entries;
using Common.Localization.Providers;

namespace Bot.DiscordRelated {
    [Serializable]
    public class CommandInterruptionException : Exception {
        public CommandInterruptionException() { }
        public CommandInterruptionException(IEntry entry) : base(entry.Get(LangLocalizationProvider.EnglishLocalizationProvider)) { }
        public CommandInterruptionException(string message) : base(message) { }
        public CommandInterruptionException(string message, Exception inner) : base(message, inner) { }
        protected CommandInterruptionException(
            SerializationInfo info,
            StreamingContext context) : base(info, context) { }
    }
}