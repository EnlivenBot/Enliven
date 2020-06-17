using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Bot.Config.Localization.Providers;
using Bot.Music;
using Bot.Utilities.Commands;
using HarmonyLib;
using LiteDB;

#pragma warning disable 8632

namespace Bot.Config {
    // ReSharper disable once InconsistentNaming
    public static class GlobalDB {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static LiteDatabase Database;

        static GlobalDB() {
            Database = InitializeDatabase();
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

        private static LiteDatabase InitializeDatabase() {
            logger.Info("Loading database");
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"))) {
                Directory.CreateDirectory("Config");
                File.Move(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            }

            var tempdb = LoadDatabase();
            tempdb.CheckpointSize = 1000;

            PerformUpgrades(tempdb);
            tempdb.Checkpoint();

            _checkpointTimer = new Timer(state => tempdb.Checkpoint(), null, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(30));
            _rebuildTimer = new Timer(state => tempdb.Rebuild(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(180));
            logger.Info("Database loaded");
            return tempdb;
        }

        private static LiteDatabase LoadDatabase() {
            return new LiteDatabase(GetDatabasePath());
        }

        private static string GetDatabasePath() {
            return Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db");
        }

        private static void PerformUpgrades(LiteDatabase liteDatabase) {
            logger.Info("Looking for database upgrades");
            var liteDatabaseCheckpointSize = liteDatabase.CheckpointSize;
            liteDatabase.CheckpointSize = 0;
            var upgrades = Assembly.GetExecutingAssembly().GetTypes()
                                   .SelectMany(AccessTools.GetDeclaredMethods)
                                   .Where(m => m.GetCustomAttributes(typeof(DbUpgradeAttribute), false).Length > 0)
                                    // ReSharper disable once PossibleNullReferenceException
                                   .Select(info => ((DbUpgradeAttribute) info.GetCustomAttribute(typeof(DbUpgradeAttribute)), info))
                                   .OrderBy(tuple => tuple.Item1.Version)
                                   .ToList();
            foreach (var upgrade in upgrades.SkipWhile((tuple, i) => tuple.Item1.Version <= liteDatabase.UserVersion)) {
                logger.Info("Upgrading database to version {version}", upgrade.Item1.Version);

                if (upgrade.Item1.TransactionsFriendly) {
                    liteDatabase.BeginTrans();
                }
                else {
                    logger.Info("Upgrade does not support transactions. We make a backup.");
                    liteDatabase.Checkpoint();
                    liteDatabase.Dispose();
                    File.Copy(GetDatabasePath(), Path.ChangeExtension(GetDatabasePath(), ".bak"), true);
                    logger.Info("Backup maked");
                    liteDatabase = LoadDatabase();
                }

                try {
                    upgrade.info.Invoke(null, new object?[] {liteDatabase});
                    if (upgrade.Item1.TransactionsFriendly) {
                        liteDatabase.Commit();
                    }
                }
                catch (Exception e) {
                    logger.Fatal(e, "Error while upgrading database");
                    logger.Fatal("Rollbacking changes");
                    if (upgrade.Item1.TransactionsFriendly) {
                        liteDatabase.Rollback();
                    }
                    else {
                        liteDatabase.Checkpoint();
                        liteDatabase.Dispose();
                        File.Copy(Path.ChangeExtension(GetDatabasePath(), ".bak"), GetDatabasePath(), true);
                    }

                    throw;
                }
                finally {
                    if (!upgrade.Item1.TransactionsFriendly) {
                        try {
                            File.Delete(Path.ChangeExtension(GetDatabasePath(), ".bak"));
                        }
                        catch (Exception e) {
                            // ignored
                        }
                    }
                }

                liteDatabase.UserVersion = upgrade.Item1.Version;
                logger.Info("Database upgraded to version {version}. Making a checkpoint", upgrade.Item1.Version);
                liteDatabase.Checkpoint();
                liteDatabase.Rebuild();
                logger.Info("Checkpoint done");
            }

            liteDatabase.CheckpointSize = liteDatabaseCheckpointSize;
        }

        #pragma warning disable 618
        [DbUpgradeAttribute(2, false)]
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

        [DbUpgradeAttribute(3, false)]
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

        [DbUpgrade(4)]
        private static void UpgradeTo4(LiteDatabase liteDatabase) {
            if (liteDatabase.UserVersion == 3) {
                var regex0 = new Regex(Regex.Escape("-1,17"));
                var regex1 = new Regex(Regex.Escape("\n-###Unavailable$$$"));
                logger.Info("Upgrading database to version 4");
                var messages = liteDatabase.GetCollection<MessageHistory>(@"MessagesHistory");
                foreach (var messageHistory in messages.FindAll().ToList()) {
                    if (messageHistory.Edits.Count == 0) {
                        messages.Delete(messageHistory.Id);
                    }
                    else {
                        if (messageHistory.Edits[0].Value != "@@ -0,0 +1,17 @@\n+###Unavailable$$$") continue;
                        if (messageHistory.Edits.Count == 1) {
                            messages.Delete(messageHistory.Id);
                        }
                        else {
                            messageHistory.IsHistoryUnavailable = true;
                            messageHistory.Edits.RemoveAt(0);
                            var value = messageHistory.Edits[0].Value;
                            value = regex0.Replace(value, "-0,0", 1);
                            value = regex1.Replace(value, "", 1);
                            if (value == "@@ -0,0 +0,0 @@") {
                                messages.Delete(messageHistory.Id);
                            }
                            else {
                                messageHistory.Edits[0].Value = value;
                                var changes = messageHistory.GetSnapshots(new LangLocalizationProvider("en"), false);
                                messages.Upsert(messageHistory);
                            }
                        }
                    }
                }
                
                logger.Info("Database upgraded to version 4. Making a checkpoint");
                liteDatabase.Checkpoint();
                liteDatabase.Rebuild();
                logger.Info("Checkpoint done");
            }
        }
        #pragma warning restore 618

        [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
        private sealed class DbUpgradeAttribute : Attribute {
            public int Version { get; }
            public bool TransactionsFriendly { get; }

            // See the attribute guidelines at 
            //  http://go.microsoft.com/fwlink/?LinkId=85236
            public DbUpgradeAttribute(int version, bool transactionsFriendly = true) {
                Version = version;
                TransactionsFriendly = transactionsFriendly;
            }
        }
    }
}