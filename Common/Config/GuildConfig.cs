using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Common.Localization.Providers;
using LiteDB;

namespace Common.Config {
    public interface IGuildConfigProvider {
        GuildConfig Get(ulong guildId);
    }

    public class GuildConfigProvider : IGuildConfigProvider {
        private ILiteCollection<GuildConfig> _collection;

        private ConcurrentDictionary<ulong, GuildConfig> _configCache = new ConcurrentDictionary<ulong, GuildConfig>();

        public GuildConfigProvider(ILiteCollection<GuildConfig> collection) {
            _collection = collection;
        }

        public GuildConfig Get(ulong guildId) {
            return _configCache.GetOrAdd(guildId, arg => {
                var guildConfig = _collection.FindById((long)arg);
                if (guildConfig == null) {
                    guildConfig = new GuildConfig() { GuildId = guildId };
                    _collection.Upsert(guildConfig);
                }

                guildConfig.SaveRequest.Subscribe(config => _collection.Upsert(config));
                return guildConfig;
            });
        }
    }

    public partial class GuildConfig {
        [BsonIgnore] private ILocalizationProvider? _loc;
        [BsonIgnore] private GuildPrefixProvider? _prefixProvider;
        [BsonId] public ulong GuildId { get; set; }
        public string Prefix { get; set; } = "&";
        public int Volume { get; set; } = 100;
        public string GuildLanguage { get; set; } = "en";
        public bool IsMusicLimited { get; set; }

        public bool IsLoggingEnabled { get; set; } = false;
        public bool IsCommandLoggingEnabled { get; set; } = false;
        public bool HistoryMissingInLog { get; set; }
        public bool HistoryMissingPacks { get; set; }
        public List<ulong> LoggedChannels { get; set; } = new List<ulong>();
        public MessageExportType MessageExportType { get; set; } = MessageExportType.Html;

        public ConcurrentDictionary<ChannelFunction, ulong> FunctionalChannels { get; set; } = new ConcurrentDictionary<ChannelFunction, ulong>();
    }

    public enum ChannelFunction {
        Log,
        Music
    }

    public enum MessageExportType {
        Image,
        Html
    }

    public partial class GuildConfig {
        [BsonIgnore] private readonly Subject<GuildConfig> _saveRequest = new();
        [BsonIgnore] public IObservable<GuildConfig> SaveRequest => _saveRequest.AsObservable();

        [BsonIgnore] private readonly Subject<GuildConfig> _localizationChanged = new();
        [BsonIgnore] public IObservable<GuildConfig> LocalizationChanged => _localizationChanged.AsObservable();

        [BsonIgnore] private readonly Subject<ulong> _channelLoggingChanged = new();
        [BsonIgnore] public IObservable<ulong> ChannelLoggingChanged => _channelLoggingChanged.AsObservable();

        [BsonIgnore] private readonly Subject<ChannelFunction> _functionalChannelsChanged = new();
        [BsonIgnore] public IObservable<ChannelFunction> FunctionalChannelsChanged => _functionalChannelsChanged.AsObservable();

        [BsonIgnore] public ILocalizationProvider Loc => _loc ??= new GuildLocalizationProvider(this);
        [BsonIgnore] public GuildPrefixProvider PrefixProvider => _prefixProvider ??= new GuildPrefixProvider(this);
        
        [BsonIgnore] public bool SendWithoutHistoryPacks => HistoryMissingInLog && HistoryMissingPacks;

        public void Save() {
            _saveRequest.OnNext(this);
        }

        public GuildConfig SetChannel(string channelId, ChannelFunction func) {
            if (channelId == "null")
                FunctionalChannels.TryRemove(func, out _);
            else
                FunctionalChannels[func] = Convert.ToUInt64(channelId);

            if (func == ChannelFunction.Log) {
                ToggleChannelLogging(Convert.ToUInt64(channelId));
            }

            _functionalChannelsChanged.OnNext(func);

            return this;
        }

        public bool GetChannel(ChannelFunction function, out ulong channelId) => FunctionalChannels.TryGetValue(function, out channelId);

        public string GetLanguage() {
            return GuildLanguage;
        }

        public GuildConfig SetLanguage(string language) {
            GuildLanguage = language;
            _localizationChanged.OnNext(this);
            return this;
        }

        public void ToggleChannelLogging(ulong channelId, bool? enable = null) {
            var contains = LoggedChannels.Contains(channelId);
            if (enable == contains) return;
            if (enable ?? !contains) {
                LoggedChannels.Add(channelId);
            }
            else {
                LoggedChannels.Remove(channelId);
            }
            _channelLoggingChanged.OnNext(channelId);
        }
    }
}