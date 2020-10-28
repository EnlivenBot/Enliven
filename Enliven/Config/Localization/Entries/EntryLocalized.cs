using System;
using Bot.Config.Localization.Providers;

namespace Bot.Config.Localization.Entries {
    public class EntryLocalized : EntryString {
        public EntryLocalized(string id) : base(id) { }

        public EntryLocalized(string id, params object[] args) : base(id, args) { }
        public EntryLocalized(string id, params Func<object>[] args) : base(id, args) { }

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