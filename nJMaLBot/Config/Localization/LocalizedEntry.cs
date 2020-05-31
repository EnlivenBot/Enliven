using System;
using System.Collections.Generic;
using System.Linq;
using Bot.Config.Localization.Providers;
using Bot.Utilities;

namespace Bot.Config.Localization {
    public class LocalizedEntry {
        public LocalizedEntry(string @group, string id) {
            Group = @group;
            Id = id;
        }

        public LocalizedEntry(string id) : this(id.Split(".")[0], id.Split(".")[1]) { }

        private bool _isCalculated = false;
        private List<Func<object>> FormatArgs { get; set; } = new List<Func<object>>();
        
        public string Group { get; set; }
        public string Id { get; set; }

        private ILocalizationProvider _provider;
        private string _cache;
        public string Get(ILocalizationProvider provider) {
            if (_isCalculated || _provider != provider) {
                _cache = provider.Get(Group, Id).Format(FormatArgs.Select(func => func()));
                _provider = provider;
            }
            
            return _cache;
        }

        public LocalizedEntry Add(params string[] args) {
            FormatArgs.AddRange(args.ToList().Select<string, Func<string>>(s => () => s));

            return this;
        }
        
        public LocalizedEntry Add(params Func<string>[] args) {
            _isCalculated = true;
            FormatArgs.AddRange(args);
            return this;
        }
    }
}