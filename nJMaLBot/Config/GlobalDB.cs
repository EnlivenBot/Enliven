using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Bot.Music;
using Bot.Utilities.Commands;
using HarmonyLib;
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
            tempdb.CheckpointSize = 1000;

            PerformUpgrades(tempdb);
            
            _checkpointTimer = new Timer(state => tempdb.Checkpoint(), null, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(30));
            _rebuildTimer = new Timer(state => tempdb.Rebuild(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(180));
            logger.Info("Database loaded");
            return tempdb;
        }

        private static void PerformUpgrades(LiteDatabase liteDatabase) {
            logger.Info("Looking for database upgrades");
            var liteDatabaseCheckpointSize = liteDatabase.CheckpointSize;
            liteDatabase.CheckpointSize = 0;
            var upgrades = Assembly.GetExecutingAssembly().GetTypes()
                                   .SelectMany(AccessTools.GetDeclaredMethods)
                                   .Where(m => m.GetCustomAttributes(typeof(DbUpgradeAttribute), false).Length > 0)
                                    // ReSharper disable once PossibleNullReferenceException
                                   .Select(info => (((DbUpgradeAttribute) info.GetCustomAttribute(typeof(DbUpgradeAttribute))).Version, info))
                                   .OrderBy(tuple => tuple.Version)
                                   .ToList();
            foreach (var upgrade in upgrades.SkipWhile((tuple, i) => tuple.Version <= liteDatabase.UserVersion)) {
                logger.Info("Upgrading database to version {version}", upgrade.Version);
                try {
                    liteDatabase.BeginTrans();
                    #pragma warning disable 8632
                    upgrade.info.Invoke(null, new object?[] {liteDatabase});
                    #pragma warning restore 8632
                    liteDatabase.Commit();

                    liteDatabase.UserVersion = upgrade.Version;
                    logger.Info("Database upgraded to version {version}. Making a checkpoint", upgrade.Version);
                    liteDatabase.Checkpoint();
                    liteDatabase.Rebuild();
                    logger.Info("Checkpoint done");
                }
                catch (Exception e) {
                    logger.Fatal(e, "Error while upgrading database");
                    logger.Fatal("Rollbacking changes");
                    liteDatabase.Rollback();
                    throw;
                }
            }
            
            liteDatabase.CheckpointSize = liteDatabaseCheckpointSize;
        }

        #pragma warning disable 618
        [DbUpgradeAttribute(2)]
        private static void UpgradeTo2(LiteDatabase liteDatabase) {
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

        [DbUpgradeAttribute(3)]
        private static void UpgradeTo3(LiteDatabase liteDatabase) {
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
        #pragma warning restore 618

        [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
        private sealed class DbUpgradeAttribute : Attribute {
            public int Version { get; set; }

            // See the attribute guidelines at 
            //  http://go.microsoft.com/fwlink/?LinkId=85236
            public DbUpgradeAttribute(int version) {
                Version = version;
            }
        }
    }
}