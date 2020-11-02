using System;
using System.Collections.Concurrent;
using Discord;
using LiteDB;

namespace Common.Config {
    public partial class UserData {
        [BsonId]
        public ulong UserId { get; set; }

        public string? LastKnownUsername { get; set; }
        
        [BsonIgnore] public bool IsCurrentUser => UserId == 0;
    }

    public partial class UserData {
        public string GetMention() => $"<@{UserId}>";
        
        public string GetMentionWithUsername() {
            return GetMentionWithUsernameInternal(LastKnownUsername);
        }

        public string GetMentionWithUsername(IUser? user) {
            return user == null ? GetMentionWithUsername() : GetMentionWithUsernameInternal(user.Username);
        }
        
        private string GetMentionWithUsernameInternal(string username) {
            return username != null ? $"{GetMention()} ({username})" : GetMention();
        }
    }

    public partial class UserData {
        public static UserData Current => Get(0);
        
        private static ConcurrentDictionary<ulong, UserData> _configCache = new ConcurrentDictionary<ulong, UserData>();

        public static UserData Get(ulong userId) {
            return _configCache.GetOrAdd(userId, arg => {
                var guildConfig = Database.Users.FindById((long) arg);
                return guildConfig ?? TryCreate(userId);
            });
        }

        public static UserData TryCreate(ulong userId) {
            var guildConfig = Database.Users.FindById(userId);
            if (guildConfig != null) {
                return guildConfig;
            }

            guildConfig = new UserData {UserId = userId};
            guildConfig.Save();

            return guildConfig;
        }

        public void Save() {
            Database.Users.Upsert(this);
        }

        public static void MatchWithUser(IUser user) {
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

        public static UserData FromUser(IUser user) {
            var data = Get(user.Id);
            MatchWithUser(user);
            
            return data;
        }

        public UserLink ToLink() {
            return new UserLink(UserId);
        }
    }

    public class UserLink {
        [BsonIgnore] public static UserLink Current => new UserLink(0);
        
        [Obsolete("")]
        public UserLink() { }
        
        public UserLink(ulong userId) {
            UserId = userId;
        }
        
        [BsonId] public ulong UserId { get; set; }

        [BsonIgnore] public UserData Data => UserData.Get(UserId);

        [BsonIgnore] public bool IsCurrentUser => UserId == 0;
    }
}