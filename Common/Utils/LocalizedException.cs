using System;
using System.Runtime.Serialization;
using Common.Localization.Entries;
using Common.Localization.Providers;

namespace Common.Utils {
    [Serializable]
    public class LocalizedException : Exception, IEntry {
        private IEntry _entry;
        
        public LocalizedException(IEntry entry) {
            _entry = entry;
        }

        public LocalizedException(string message) {
            _entry = new EntryString(message);
        }
        
        public LocalizedException(IEntry entry, Exception inner) : base(entry.Get(LangLocalizationProvider.EnglishLocalizationProvider), inner) {
            _entry = entry;
        }
        
        public LocalizedException(string message, Exception inner) : base(message, inner) {
            _entry = new EntryString(message);
        }
        
        public string Get(ILocalizationProvider provider, params object[] additionalArgs) {
            return _entry.Get(provider, additionalArgs);
        }
    }
}