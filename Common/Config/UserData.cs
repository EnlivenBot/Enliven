using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Discord;
using LiteDB;

namespace Common.Config {
    public interface IUserDataProvider {
        UserData Get(ulong userId);
        void MatchWithUser(IUser user);
        UserData FromUser(IUser user);
    }

    public class UserDataProvider : IUserDataProvider {
        private  ConcurrentDictionary<ulong, UserData> _configCache = new ConcurrentDictionary<ulong, UserData>();
        private ILiteCollection<UserData> _liteCollection;

        public UserDataProvider(ILiteCollection<UserData> liteCollection) {
            _liteCollection = liteCollection;
        }

        public UserData Get(ulong userId) {
            return _configCache.GetOrAdd(userId, arg => {
                var userData = _liteCollection.FindById((long) arg);
                if (userData == null) {
                    userData = new UserData {UserId = userId};
                    _liteCollection.Upsert(userData);
                }

                userData.SaveRequest.Subscribe(data => _liteCollection.Upsert(userData));
                
                return userData;
            });
        }
        
        public void MatchWithUser(IUser user) {
            var data = Get(user.Id);
            try {
                if (!string.IsNullOrWhiteSpace(user.Username)) {
                    data.LastKnownUsername = user.Username;
                }

                data.Save();
            }
            catch (Exception) {
                // ignored
            }
        }

        public UserData FromUser(IUser user) {
            var data = Get(user.Id);
            MatchWithUser(user);

            return data;
        }
    }

    public class UserData {
        [BsonId] public ulong UserId { get; set; }

        public string? LastKnownUsername { get; set; }

        [BsonIgnore] public bool IsCurrentUser => UserId == 0;

        public string GetMention() => $"<@{UserId}>";

        public string GetMentionWithUsername() {
            return GetMentionWithUsernameInternal(LastKnownUsername);
        }

        public string GetMentionWithUsername(IUser? user) {
            return user == null ? GetMentionWithUsername() : GetMentionWithUsernameInternal(user.Username);
        }

        private string GetMentionWithUsernameInternal(string? username) {
            return username != null ? $"{GetMention()} ({username})" : GetMention();
        }

        [BsonIgnore] private readonly ISubject<UserData> _saveRequest = new Subject<UserData>();
        [BsonIgnore] public IObservable<UserData> SaveRequest => _saveRequest.AsObservable();
        public void Save() {
            _saveRequest.OnNext(this);
        }

        public UserLink ToLink() {
            return new UserLink(UserId);
        }
    }

    public class UserLink {
        [BsonIgnore] public static UserLink Current => new UserLink(0);

        [Obsolete("This is constructor for database engine")]
        public UserLink() { }

        public UserLink(ulong userId) {
            UserId = userId;
        }

        [BsonId] public ulong UserId { get; set; }

        public UserData GetData(IUserDataProvider dataProvider) {
            return dataProvider.Get(UserId);
        }

        [BsonIgnore] public bool IsCurrentUser => UserId == 0;
    }
}