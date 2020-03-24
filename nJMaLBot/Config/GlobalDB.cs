using System;
using System.IO;
using Bot.Music;
using LiteDB;

namespace Bot.Config {
    public class GlobalDB {
        private static readonly Lazy<LiteDatabase> _database = new Lazy<LiteDatabase>(Init);

        public static readonly LiteCollection<Entity> GlobalSettings = Database.GetCollection<Entity>(@"Global");
        public static readonly LiteCollection<GuildConfig> Guilds = Database.GetCollection<GuildConfig>(@"Guilds");
        public static readonly LiteCollection<BsonDocument> IgnoredMessages = Database.GetCollection<BsonDocument>(@"IgnoredMessages");
        public static readonly LiteCollection<MessageHistory> Messages = Database.GetCollection<MessageHistory>(@"MessagesHistory");
        public static readonly LiteCollection<StatisticsPart> CommandStatistics = Database.GetCollection<StatisticsPart>(@"CommandStatistics");
        public static readonly LiteCollection<StoredPlaylist> Playlists = Database.GetCollection<StoredPlaylist>(@"StoredPlaylists");
        public static LiteDatabase Database => _database.Value;

        private static LiteDatabase Init() {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"))) {
                Directory.CreateDirectory("Config");
                File.Move(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"), 
                    Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            }

            var tempdb = new LiteDatabase(Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            //Updating database in future
            tempdb.UserVersion = 1;
            return tempdb;
        }
    }
}