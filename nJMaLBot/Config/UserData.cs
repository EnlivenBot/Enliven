using System;
using System.Collections.Concurrent;
using Discord;
using LiteDB;

namespace Bot.Config {
    public partial class UserData {
        [BsonId]
        public ulong UserId { get; set; }

        public string? LastKnownUsername { get; set; }

        [BsonIgnore] public bool IsCurrentUser => Program.Client.CurrentUser.Id == UserId;
    }

    public partial class UserData {
        public string GetMention(bool includeUsername) {
            var mention = $"<@{UserId}>";
            if (!includeUsername) return mention;
            var user = GetUser();
            if (user != null)
                return $"{mention} ({user.Username})";
            return LastKnownUsername != null ? $"{mention} ({LastKnownUsername})" : mention;
        }

        public IUser? GetUser() {
            try {
                return Program.Client.GetUser(UserId);
            }
            catch (Exception) {
                return null;
            }
        }
    }

    public partial class UserData {
        public static UserData Current => Get(0);
        
        private static ConcurrentDictionary<ulong, UserData> _configCache = new ConcurrentDictionary<ulong, UserData>();

        public static UserData Get(ulong userId) {
            if (userId == 0) userId = Program.Client.CurrentUser.Id;
            return _configCache.GetOrAdd(userId, arg => {
                var guildConfig = GlobalDB.Users.FindById((long) arg);
                return guildConfig ?? TryCreate(userId);
            });
        }

        public static UserData GetCurrentUser() {
            return Get(Program.Client.CurrentUser.Id);
        }

        public static UserData TryCreate(ulong userId) {
            var guildConfig = GlobalDB.Users.FindById(userId);
            if (guildConfig != null) {
                return guildConfig;
            }

            guildConfig = new UserData {UserId = userId};
            guildConfig.Save();

            return guildConfig;
        }

        public void Save() {
            GlobalDB.Users.Upsert(this);
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
            if (userId == 0) userId = Program.Client.CurrentUser.Id;
            UserId = userId;
        }
        
        [BsonId] public ulong UserId { get; set; }

        [BsonIgnore] public UserData Data => UserData.Get(UserId);

        [BsonIgnore] public bool IsCurrentUser => UserId == Program.Client.CurrentUser.Id;
    }
}