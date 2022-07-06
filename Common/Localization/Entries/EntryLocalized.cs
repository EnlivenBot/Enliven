using System;
using Common.Localization.Providers;

namespace Common.Localization.Entries {
    public class EntryLocalized : EntryString {
        public EntryLocalized(string id) : base(id) { }

        public EntryLocalized(string id, params object[] args) : base(id, args) { }
        public EntryLocalized(string id, params Func<object>[] args) : base(id, args) { }

        public static EntryLocalized Create(string group, string id) {
            return new EntryLocalized($"{group}.{id}");
        }

        public override bool CanGet() {
            return LocalizationManager.IsLocalizationExists(Content);
        }

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