using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Common.Localization.Providers;
using LiteDB;

namespace Common.Config;

public interface IGuildConfigProvider
{
    GuildConfig Get(ulong guildId);
}

public class GuildConfigProvider : IGuildConfigProvider
{
    private ILiteCollection<GuildConfig> _collection;

    private ConcurrentDictionary<ulong, GuildConfig> _configCache = new();

    public GuildConfigProvider(ILiteCollection<GuildConfig> collection)
    {
        _collection = collection;
    }

    public GuildConfig Get(ulong guildId)
    {
        return _configCache.GetOrAdd(guildId, arg =>
        {
            var guildConfig = _collection.FindById((long)arg);
            if (guildConfig == null)
            {
                guildConfig = new GuildConfig() { GuildId = guildId };
                _collection.Upsert(guildConfig);
            }

            guildConfig.SaveRequest.Subscribe(config => _collection.Upsert(config));
            return guildConfig;
        });
    }
}

public partial class GuildConfig
{
    [BsonIgnore] private ILocalizationProvider? _loc;
    [BsonId] public ulong GuildId { get; set; }
    public string Prefix { get; set; } = "&";
    public int Volume { get; set; } = 100;
    public string GuildLanguage { get; set; } = "en";
    public bool IsMusicLimited { get; set; }

    public ConcurrentDictionary<ChannelFunction, ulong> FunctionalChannels { get; set; } = new();
}

public enum ChannelFunction
{
    [Obsolete]
    Log,
    Music,
    DedicatedMusic
}

public partial class GuildConfig
{

    [BsonIgnore] private readonly Subject<ChannelFunction> _functionalChannelsChanged = new();

    [BsonIgnore] private readonly Subject<GuildConfig> _localizationChanged = new();
    [BsonIgnore] private readonly Subject<GuildConfig> _saveRequest = new();
    [BsonIgnore] public IObservable<GuildConfig> SaveRequest => _saveRequest.AsObservable();
    [BsonIgnore] public IObservable<GuildConfig> LocalizationChanged => _localizationChanged.AsObservable();

    [BsonIgnore]
    public IObservable<ChannelFunction> FunctionalChannelsChanged => _functionalChannelsChanged.AsObservable();

    [BsonIgnore] public ILocalizationProvider Loc => _loc ??= new GuildLocalizationProvider(this);

    public void Save()
    {
        _saveRequest.OnNext(this);
    }

    public GuildConfig SetChannel(ChannelFunction func, ulong? channelId)
    {
        if (!channelId.HasValue)
            FunctionalChannels.TryRemove(func, out _);
        else
            FunctionalChannels[func] = Convert.ToUInt64(channelId);

        _functionalChannelsChanged.OnNext(func);

        return this;
    }

    public bool GetChannel(ChannelFunction function, out ulong channelId) =>
        FunctionalChannels.TryGetValue(function, out channelId);

    public string GetLanguage()
    {
        return GuildLanguage;
    }

    public GuildConfig SetLanguage(string language)
    {
        GuildLanguage = language;
        _localizationChanged.OnNext(this);
        return this;
    }
}