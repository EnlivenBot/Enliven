using System;
using System.Collections.Concurrent;
using Discord;
using LiteDB;

namespace Bot.Config {
    public partial class UserData {
        [BsonId]
        public ulong UserId { get; set; }

        public string? LastKnownUsername { get; set; }
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
        private static ConcurrentDictionary<ulong, UserData> _configCache = new ConcurrentDictionary<ulong, UserData>();

        public static UserData Get(ulong userId) {
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
    }

    public class UserLink {
        [BsonId] public ulong UserId { get; set; }

        [BsonIgnore] public UserData Data => UserData.Get(UserId);
    }
}