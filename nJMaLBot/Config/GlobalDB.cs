using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bot.Music;
using Bot.Utilities.Commands;
using LiteDB;
using LiteDB.Engine;

namespace Bot.Config {
    public class GlobalDB {
        private static readonly Lazy<LiteDatabase> _database = new Lazy<LiteDatabase>(Init);

        public static readonly ILiteCollection<Entity> GlobalSettings = Database.GetCollection<Entity>(@"Global");
        public static readonly ILiteCollection<GuildConfig> Guilds = Database.GetCollection<GuildConfig>(@"Guilds");
        public static readonly ILiteCollection<BsonDocument> IgnoredMessages = Database.GetCollection<BsonDocument>(@"IgnoredMessages");
        public static readonly ILiteCollection<MessageHistory> Messages = Database.GetCollection<MessageHistory>(@"MessagesHistory");
        public static readonly ILiteCollection<StatisticsPart> CommandStatistics = Database.GetCollection<StatisticsPart>(@"CommandStatistics");
        public static readonly ILiteCollection<StoredPlaylist> Playlists = Database.GetCollection<StoredPlaylist>(@"StoredPlaylists");
        public static LiteDatabase Database => _database.Value;

        private static LiteDatabase Init() {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"))) {
                Directory.CreateDirectory("Config");
                File.Move(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            }

            var tempdb = new LiteDatabase(Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            UpgradeTo2(tempdb);
            tempdb.UserVersion = 2;

            tempdb.Checkpoint();
            tempdb.Rebuild();
            return tempdb;
        }

        private static void UpgradeTo2(LiteDatabase liteDatabase) {
            if (liteDatabase.UserVersion == 1) {
                var oldStatsCollection = liteDatabase.GetCollection<ObsoleteStatisticsPart>(@"CommandStatistics");
                var oldStats = oldStatsCollection.FindAll().ToList();
                var newStats = oldStats.Select(part => new StatisticsPart {
                    Id = part.Id, UsagesList = (long.TryParse(part.Id, out _) || part.Id == "Global"
                            ? part.UsagesList.Where(pair => HelpUtils.CommandAliases.Value.Contains(pair.Key))
                            : part.UsagesList)
                       .ToDictionary(pair => pair.Key, pair => (int) pair.Value)
                });
                liteDatabase.DropCollection(@"CommandStatistics");
                var statsCollection = liteDatabase.GetCollection<StatisticsPart>(@"CommandStatistics");
                statsCollection.InsertBulk(newStats);
            }
        }
    }
}