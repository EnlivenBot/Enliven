using System;
using System.Collections.Generic;
using System.Linq;
using Common.Localization.Providers;

namespace Common.Localization.Entries {
    public class EntryString : EntryBase {
        public string Content { get; set; }

        public EntryString(string content) {
            Content = content;
        }

        public EntryString(string content, params object[] args) : this(content) {
            FormatArgs = args.Select(o => new Func<object>(() => o)).ToList();
        }

        public EntryString(string content, params Func<object>[] args) : this(content) {
            FormatArgs = args.ToList();
            _isCalculated = true;
        }

        private bool _isCalculated;
        private List<Func<object>> FormatArgs { get; set; } = new List<Func<object>>();

        public EntryString Add(params string[] args) {
            FormatArgs.AddRange(args.ToList().Select<string, Func<string>>(s => () => s));

            return this;
        }

        public EntryString Add(params Func<string>[] args) {
            _isCalculated = true;
            FormatArgs.AddRange(args);
            return this;
        }

        private protected virtual string GetFormatString(ILocalizationProvider provider) {
            return Content;
        }

        private ILocalizationProvider _lastProvider = null!;
        private string _cache = null!;

        public override string Get(ILocalizationProvider provider, params object[] additionalArgs) {
            if (_isCalculated || _lastProvider != provider || additionalArgs.Length != 0) {
                var formatArgs = FormatArgs
                    .Select(func => func().Pipe(o => o is IEntry loc ? loc.Get(provider) : o))
                    .Concat(additionalArgs)
                    .Select(o => o is IEntry entry ? entry.Get(provider) : o)
                    .ToArray();
                _cache = GetFormatString(provider).Pipe(s => formatArgs.Length == 0 ? s : string.Format(s, formatArgs));
                _lastProvider = provider;
            }

            return _cache;
        }

        public static implicit operator EntryString(string s) {
            return new EntryString(s);
        }
    }
}