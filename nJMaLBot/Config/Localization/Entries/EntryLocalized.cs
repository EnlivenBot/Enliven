using System;
using Bot.Config.Localization.Providers;

namespace Bot.Config.Localization.Entries {
    public class EntryLocalized : EntryString {
        public EntryLocalized(string content) : base(content) { }

        public EntryLocalized(string content, params object[] args) : base(content, args) { }
        public EntryLocalized(string content, params Func<object>[] args) : base(content, args) { }

        private protected override string GetFormatString(ILocalizationProvider provider) {
            return provider.Get(Content);
        }

        public new EntryLocalized Add(params string[] args) {
            base.Add(args);
            return this;
        }

        public new EntryLocalized Add(params Func<string>[] args) {
            base.Add(args);
            return this;
        }
    }
}