﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Discord;
using LiteDB;

namespace Common.Config;

public interface IUserDataProvider
{
    UserData Get(ulong userId);
    void MatchWithUser(IUser user);
    UserData FromUser(IUser user);
}

public class UserDataProvider : IUserDataProvider
{
    private ConcurrentDictionary<ulong, UserData> _configCache = new();
    private ILiteCollection<UserData> _liteCollection;

    public UserDataProvider(ILiteCollection<UserData> liteCollection)
    {
        _liteCollection = liteCollection;
    }

    public UserData Get(ulong userId)
    {
        return _configCache.GetOrAdd(userId, arg =>
        {
            var userData = _liteCollection.FindById((long)arg);
            if (userData == null)
            {
                userData = new UserData { UserId = userId };
                _liteCollection.Upsert(userData);
            }

            userData.SaveRequest.Subscribe(data => _liteCollection.Upsert(userData));

            return userData;
        });
    }

    public void MatchWithUser(IUser user)
    {
        var data = Get(user.Id);
        try
        {
            if (!string.IsNullOrWhiteSpace(user.Username))
            {
                data.LastKnownUsername = user.Username;
            }

            data.Save();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public UserData FromUser(IUser user)
    {
        var data = Get(user.Id);
        MatchWithUser(user);

        return data;
    }
}

public class UserData
{
    [BsonIgnore] private readonly ISubject<UserData> _saveRequest = new Subject<UserData>();
    [BsonId] public ulong UserId { get; set; }

    public string? LastKnownUsername { get; set; }

    public List<PlayerEffect> PlayerEffects { get; set; } = new();

    [BsonIgnore] public bool IsCurrentUser => UserId == 0;
    [BsonIgnore] public IObservable<UserData> SaveRequest => _saveRequest.AsObservable();

    public string GetMention() => $"<@{UserId}>";

    public string GetMentionWithUsername()
    {
        return GetMentionWithUsernameInternal(LastKnownUsername);
    }

    public string GetMentionWithUsername(IUser? user)
    {
        return user == null ? GetMentionWithUsername() : GetMentionWithUsernameInternal(user.Username);
    }

    private string GetMentionWithUsernameInternal(string? username)
    {
        return username != null ? $"{GetMention()} ({username})" : GetMention();
    }

    public void Save()
    {
        _saveRequest.OnNext(this);
    }

    public UserLink ToLink()
    {
        return new UserLink(UserId);
    }
}

public class UserLink
{
    [Obsolete("This is constructor for database engine")]
    public UserLink()
    {
    }

    public UserLink(ulong userId)
    {
        UserId = userId;
    }

    [BsonIgnore] public static UserLink Current => new(0);

    [BsonId] public ulong UserId { get; set; }

    [BsonIgnore] public bool IsCurrentUser => UserId == 0;
    [BsonIgnore] public string Mention => $"<@{UserId}>";

    public UserData GetData(IUserDataProvider dataProvider)
    {
        return dataProvider.Get(UserId);
    }
}