using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config.Localization.Providers;
using Bot.Music;
using Bot.Utilities.Commands;
using Discord;
using HarmonyLib;
using LiteDB;
using LiteDB.Engine;

#pragma warning disable 8632

namespace Bot.Config {
    // ReSharper disable once InconsistentNaming
    public static class GlobalDB {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static LiteDatabase Database;

        static GlobalDB() {
            InitializeDatabase();
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

        private static void InitializeDatabase() {
            logger.Info("Loading database");
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"))) {
                Directory.CreateDirectory("Config");
                File.Move(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            }

            Database = LoadDatabase();
            Database.CheckpointSize = 10000;

            PerformUpgrades();
            Database.Checkpoint();

            _checkpointTimer = new Timer(state => Database.Checkpoint(), null, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(30));
            _rebuildTimer = new Timer(state => Database.Rebuild(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(180));
            logger.Info("Database loaded");
        }

        private static LiteDatabase LoadDatabase() {
            return new LiteDatabase(GetDatabasePath());
        }

        private static string GetDatabasePath() {
            return Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db");
        }

        private static void PerformUpgrades() {
            logger.Info("Looking for database upgrades");
            var liteDatabaseCheckpointSize = Database.CheckpointSize;
            Database.CheckpointSize = 0;
            var upgrades = Assembly.GetExecutingAssembly().GetTypes()
                                   .SelectMany(AccessTools.GetDeclaredMethods)
                                   .Where(m => m.GetCustomAttributes(typeof(DbUpgradeAttribute), false).Length > 0)
                                    // ReSharper disable once PossibleNullReferenceException
                                   .Select(info => ((DbUpgradeAttribute) info.GetCustomAttribute(typeof(DbUpgradeAttribute))!, info))
                                   .OrderBy(tuple => tuple.Item1.Version)
                                   .ToList();
            foreach (var upgrade in upgrades.SkipWhile((tuple, i) => tuple.Item1.Version <= Database.UserVersion)) {
                logger.Info("Upgrading database to version {version}", upgrade.Item1.Version);

                if (upgrade.Item1.TransactionsFriendly) {
                    Database.BeginTrans();
                }
                else {
                    logger.Info("Upgrade does not support transactions. We make a backup.");
                    Database.Checkpoint();
                    Database.Dispose();
                    File.Copy(GetDatabasePath(), Path.ChangeExtension(GetDatabasePath(), ".bak"), true);
                    logger.Info("Backup maked");
                    Database = LoadDatabase();
                }

                try {
                    upgrade.info.Invoke(null, new object?[] {Database});
                    if (upgrade.Item1.TransactionsFriendly) {
                        Database.Commit();
                    }
                }
                catch (Exception e) {
                    logger.Fatal(e, "Error while upgrading database");
                    logger.Fatal("Rollbacking changes");
                    if (upgrade.Item1.TransactionsFriendly) {
                        Database.Rollback();
                    }
                    else {
                        Database.Checkpoint();
                        Database.Dispose();
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

                logger.Info("Making a checkpoint");
                try {
                    Database.Checkpoint();
                    Database.Rebuild();
                }
                catch (Exception e) {
                    // This is very bad
                    // We broke the database
                    logger.Fatal(e, "Database file broken");
                    logger.Fatal("Doing backup");
                    File.Copy(GetDatabasePath(), Path.Combine(Path.GetDirectoryName(GetDatabasePath())!, DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".bak"),
                        true);
                    logger.Fatal("Trying to copy intact information");
                    using (LiteDatabase newDb = new LiteDatabase("newDb.db")) {
                        foreach (var collectionName in Database.GetCollectionNames()) {
                            try {
                                var collection = newDb.GetCollection(collectionName);
                                foreach (var bsonDocument in Database.GetCollection(collectionName).FindAll()) {
                                    try {
                                        collection.Insert(bsonDocument);
                                    }
                                    catch (Exception exception) {
                                        logger.Error(exception, "Database recreation error. Collection - {collectionName}", collectionName);
                                    }
                                }
                            }
                            catch (Exception exception) {
                                logger.Error(exception, "Database recreation error. Collection - {collectionName}", collectionName);
                            }
                        }
                    }

                    Database.Dispose();
                    File.Move("newDb.db", GetDatabasePath(), true);
                    Database = LoadDatabase();
                }

                logger.Info("Checkpoint done");
                logger.Info("Database upgraded to version {version}", upgrade.Item1.Version);
                Database.UserVersion = upgrade.Item1.Version;
            }

            Database.CheckpointSize = liteDatabaseCheckpointSize;
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
            var oldIgnoredMessages = liteDatabase.GetCollection<BsonDocument>(@"IgnoredMessages");
            var sortedIgnoredMessages = oldIgnoredMessages.FindAll()
                                                          .Select(document => document.ToString().Split(':'))
                                                          .GroupBy(strings => strings[0]).Select(grouping => new ListedEntry
                                                               {Id = grouping.Key, Data = grouping.Select(strings => strings[1]).ToList()});
            liteDatabase.DropCollection(@"IgnoredMessages");
            var newIgnoredMessages = liteDatabase.GetCollection<ListedEntry>(@"IgnoredMessages");
            newIgnoredMessages.Upsert(sortedIgnoredMessages);
        }

        [DbUpgrade(4)]
        private static void UpgradeTo4(LiteDatabase liteDatabase) {
            var regex0 = new Regex(Regex.Escape("-1,17"));
            var regex1 = new Regex(Regex.Escape("\n-###Unavailable$$$"));
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
                            messages.Upsert(messageHistory);
                        }
                    }
                }
            }

            Task.Run(async () => {
                await Program.WaitStartAsync;
                foreach (var messageHistoryGroup in messages.FindAll().ToList().GroupBy(history => history.ChannelId)) {
                    try {
                        var socketChannel = Program.Client.GetChannel(messageHistoryGroup.Key) as ITextChannel;
                        if (socketChannel == null) {
                            foreach (var messageHistory in messageHistoryGroup) {
                                messages.Delete(messageHistory.Id);
                            }
                        }
                        else {
                            foreach (var messageHistory in messageHistoryGroup) {
                                try {
                                    var message = await socketChannel.GetMessageAsync(messageHistory.MessageId);
                                    if (message == null) {
                                        messages.Delete(messageHistory.Id);
                                    }
                                    else {
                                        messageHistory.AuthorId = message.Author.Id;
                                        messageHistory.Save();
                                    }
                                }
                                catch (Exception) {
                                    messages.Delete(messageHistory.Id);
                                }
                            }
                        }
                    }
                    catch (Exception) {
                        foreach (var messageHistory in messageHistoryGroup) {
                            messages.Delete(messageHistory.Id);
                        }
                    }
                }

                liteDatabase.Checkpoint();
                liteDatabase.Rebuild();
            });
        }
        #pragma warning restore 618

        [DbUpgrade(5, false)]
        private static void UpgradeTo5(LiteDatabase liteDatabase) {
            liteDatabase.DropCollection("IgnoredMessages");
        }

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