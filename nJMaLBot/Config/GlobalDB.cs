using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Bot.Music;
using Bot.Utilities.Commands;
using LiteDB;

namespace Bot.Config {
    // ReSharper disable once InconsistentNaming
    public static class GlobalDB {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static LiteDatabase Database;

        static GlobalDB() {
            Database = LoadDatabase();
            GlobalSettings = Database.GetCollection<Entity>(@"Global");
            Guilds = Database.GetCollection<GuildConfig>(@"Guilds");
            Messages = Database.GetCollection<MessageHistory>(@"MessagesHistory");
            CommandStatistics = Database.GetCollection<StatisticsPart>(@"CommandStatistics");
            Playlists = Database.GetCollection<StoredPlaylist>(@"StoredPlaylists");
        }
        
        public static void Initialize() {
            // Dummy method to initialize static properties
        }

        public static readonly ILiteCollection<Entity> GlobalSettings;
        public static readonly ILiteCollection<GuildConfig> Guilds;
        public static readonly ILiteCollection<MessageHistory> Messages;
        public static readonly ILiteCollection<StatisticsPart> CommandStatistics;
        public static readonly ILiteCollection<StoredPlaylist> Playlists;
        private static Timer _checkpointTimer;
        private static Timer _rebuildTimer;

        private static LiteDatabase LoadDatabase() {
            logger.Info("Loading database");
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"))) {
                Directory.CreateDirectory("Config");
                File.Move(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            }

            var tempdb = new LiteDatabase(Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            UpgradeTo2(tempdb);
            UpgradeTo3(tempdb);
            tempdb.UserVersion = 3;

            tempdb.CheckpointSize = 1000;
            // Seems like this ^ dont work properly
            _checkpointTimer = new Timer(state => tempdb.Checkpoint(), null, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(30));
            _rebuildTimer = new Timer(state => tempdb.Rebuild(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(180));
            logger.Info("Database loaded");
            return tempdb;
        }

        #pragma warning disable 618
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

        private static void UpgradeTo3(LiteDatabase liteDatabase) {
            if (liteDatabase.UserVersion == 2) {
                logger.Info("Upgrading database to version 3");
                var oldIgnoredMessages = liteDatabase.GetCollection<BsonDocument>(@"IgnoredMessages");
                var sortedIgnoredMessages = oldIgnoredMessages.FindAll()
                                                              .Select(document => document.ToString().Split(':'))
                                                              .GroupBy(strings => strings[0]).Select(grouping => new ListedEntry
                                                                   {Id = grouping.Key, Data = grouping.Select(strings => strings[1]).ToList()});
                liteDatabase.DropCollection(@"IgnoredMessages");
                var newIgnoredMessages = liteDatabase.GetCollection<ListedEntry>(@"IgnoredMessages");
                newIgnoredMessages.Upsert(sortedIgnoredMessages);
                
                logger.Info("Database upgraded to version 3. Making a checkpoint");
                liteDatabase.Checkpoint();
                liteDatabase.Rebuild();
                logger.Info("Checkpoint done");
            }
        }
        #pragma warning restore 618
    }
}