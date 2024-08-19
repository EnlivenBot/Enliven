using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LiteDB;

namespace Common.Config;

public interface IStatisticsPartProvider
{
    StatisticsPart Get(string id);
    int Count();
    void RegisterUsage(string command, string id);
    void RegisterMusicTime(TimeSpan timeSpan);
}

public class StatisticsPartProvider : IStatisticsPartProvider
{
    private static ConcurrentDictionary<string, StatisticsPart> _cache = new();
    private ILiteCollection<StatisticsPart> _liteCollection;

    public StatisticsPartProvider(ILiteCollection<StatisticsPart> liteCollection)
    {
        _liteCollection = liteCollection;
    }

    public StatisticsPart Get(string id)
    {
        return _cache.GetOrAdd(id, s =>
        {
            var part = _liteCollection.FindById(id) ?? new StatisticsPart { Id = s };
            part.SaveRequest.Subscribe(statisticsPart => _liteCollection.Upsert(part));
            return part;
        });
    }

    public int Count()
    {
        return _liteCollection.Count();
    }

    public void RegisterUsage(string command, string id)
    {
        var userStatistics = Get(id);
        if (!userStatistics.UsagesList.TryGetValue(command, out var userUsageCount))
        {
            userUsageCount = 0;
        }

        userStatistics.UsagesList[command] = ++userUsageCount;
        userStatistics.Save();
    }

    public void RegisterMusicTime(TimeSpan timeSpan)
    {
        var userStatistics = Get("Music");
        if (!userStatistics.UsagesList.TryGetValue("PlaybackTime", out var userUsageCount))
        {
            userUsageCount = 0;
        }

        userStatistics.UsagesList["PlaybackTime"] = (int)(userUsageCount + timeSpan.TotalSeconds);
        userStatistics.Save();
    }
}

public class StatisticsPart
{
    [BsonIgnore] private readonly ISubject<StatisticsPart> _saveRequest = new Subject<StatisticsPart>();
    [BsonId] public string Id { get; set; } = null!;
    public Dictionary<string, int> UsagesList { get; set; } = new();
    [BsonIgnore] public IObservable<StatisticsPart> SaveRequest => _saveRequest.AsObservable();

    public void Save()
    {
        _saveRequest.OnNext(this);
    }
}