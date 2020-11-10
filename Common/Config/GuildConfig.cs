using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Common.Localization.Providers;
using LiteDB;

namespace Common.Config {
    public interface IGuildConfigProvider {
        GuildConfig Get(ulong guildId);
    }

    public class GuildConfigProvider : IGuildConfigProvider {
        private ILiteCollection<GuildConfig> _collection;

        public GuildConfigProvider(ILiteCollection<GuildConfig> collection) {
            _collection = collection;
        }

        private ConcurrentDictionary<ulong, GuildConfig> _configCache = new ConcurrentDictionary<ulong, GuildConfig>();

        public GuildConfig Get(ulong guildId) {
            return _configCache.GetOrAdd(guildId, arg => {
                var guildConfig = _collection.FindById((long) arg);
                if (guildConfig == null) {
                    guildConfig = new GuildConfig() {GuildId = guildId};
                    _collection.Upsert(guildConfig);
                }

                guildConfig.SaveRequest.Subscribe(config => _collection.Upsert(config));
                return guildConfig;
            });
        }
    }

    public partial class GuildConfig {
        private ILocalizationProvider? _loc;
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
        public LogExportTypes LogExportType { get; set; } = LogExportTypes.Html;

        public ConcurrentDictionary<ChannelFunction, ulong> FunctionalChannels { get; set; } = new ConcurrentDictionary<ChannelFunction, ulong>();
        [BsonIgnore] public event EventHandler<ChannelFunction>? FunctionalChannelsChanged;

        [BsonIgnore] public ILocalizationProvider Loc => _loc ??= new GuildLocalizationProvider(this);
    }

    public enum ChannelFunction {
        Log,
        Music
    }

    public enum LogExportTypes {
        Image,
        Html
    }

    public partial class GuildConfig {
        [BsonIgnore] private readonly Subject<GuildConfig> _saveRequest = new Subject<GuildConfig>();
        [BsonIgnore] public ISubject<GuildConfig> SaveRequest => _saveRequest;

        public void Save() {
            _saveRequest.OnNext(this);
        }

        protected virtual void OnFunctionalChannelsChanged(ChannelFunction e) {
            FunctionalChannelsChanged?.Invoke(this, e);
        }

        public GuildConfig SetChannel(string channelId, ChannelFunction func) {
            if (channelId == "null")
                FunctionalChannels.TryRemove(func, out _);
            else
                FunctionalChannels[func] = Convert.ToUInt64(channelId);

            if (func == ChannelFunction.Log) {
                LoggedChannels.Remove(Convert.ToUInt64(channelId));
            }

            OnFunctionalChannelsChanged(func);

            return this;
        }

        public bool GetChannel(ChannelFunction function, out ulong channelId) => FunctionalChannels.TryGetValue(function, out channelId);

        public string GetLanguage() {
            return GuildLanguage;
        }

        [BsonIgnore] public ISubject<GuildConfig> LocalizationChanged { get; } = new Subject<GuildConfig>();

        public GuildConfig SetLanguage(string language) {
            GuildLanguage = language;
            LocalizationChanged.OnNext(this);
            return this;
        }

        public static Subject<ulong> ChannelLoggingDisabled { get; } = new Subject<ulong>();

        public void ToggleChannelLogging(ulong channelId) {
            if (LoggedChannels.Contains(channelId)) {
                LoggedChannels.Remove(channelId);
                ChannelLoggingDisabled.OnNext(channelId);
            }
            else {
                LoggedChannels.Add(channelId);
            }
        }
    }
}