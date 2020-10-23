using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Logging;
using Bot.Music;
using Bot.Utilities.Music;
using Discord;
using HarmonyLib;
using LiteDB;
using NLog;

// ReSharper disable UnusedMember.Local

#pragma warning disable 8632

namespace Bot.Config {
    // ReSharper disable once InconsistentNaming
    public static class GlobalDB {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public static LiteDatabase Database = null!;

        static GlobalDB() {
            InitializeDatabase();
            GlobalSettings = Database.GetCollection<Entity>(@"Global");
            Guilds = Database.GetCollection<GuildConfig>(@"Guilds");
            Messages = Database.GetCollection<MessageHistory>(@"MessagesHistory");
            CommandStatistics = Database.GetCollection<StatisticsPart>(@"CommandStatistics");
            Playlists = Database.GetCollection<StoredPlaylist>(@"StoredPlaylists");
            SpotifyAssociations = Database.GetCollection<SpotifyTrackAssociation>(@"SpotifyAssociations");
            Users = Database.GetCollection<UserData>("UserData");
            
            //Fix for mapping TimeSpan?
            BsonMapper.Global.RegisterType
            (
                timeSpan => BsonMapper.Global.Serialize(timeSpan?.Ticks),
                bson => bson == null ?(TimeSpan?) null : TimeSpan.FromTicks((long) bson.RawValue)
            );
        }

        public static void Initialize() {
            // Dummy method to initialize static properties
        }

        public static readonly ILiteCollection<Entity> GlobalSettings;
        public static readonly ILiteCollection<GuildConfig> Guilds;
        public static readonly ILiteCollection<MessageHistory> Messages;
        public static readonly ILiteCollection<StatisticsPart> CommandStatistics;
        public static readonly ILiteCollection<StoredPlaylist> Playlists;
        public static readonly ILiteCollection<SpotifyTrackAssociation> SpotifyAssociations;
        public static readonly ILiteCollection<UserData> Users;

        // ReSharper disable once NotAccessedField.Local
        private static Timer _checkpointTimer = null!;

        // ReSharper disable once NotAccessedField.Local
        private static Timer _rebuildTimer = null!;

        private static void InitializeDatabase() {
            logger.Info("Loading database");
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"))) {
                Directory.CreateDirectory("Config");
                File.Move(Path.Combine(Directory.GetCurrentDirectory(), @"DataBase.db"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db"));
            }

            Database = LoadDatabase();
            Database.CheckpointSize = 10000;

            PerformUpgrades().Wait();
            Database.Checkpoint();

            _checkpointTimer = new Timer(state => Database.Checkpoint(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));
            _rebuildTimer = new Timer(state => Database.Rebuild(), null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(180));
            logger.Info("Database loaded");
        }

        private static LiteDatabase LoadDatabase() {
            return new LiteDatabase(GetDatabasePath());
        }

        private static string GetDatabasePath() {
            return Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db");
        }

        private static async Task PerformUpgrades() {
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
                    var upgradeResult = upgrade.info.Invoke(null, new object?[] {Database});
                    if (upgradeResult is Task upgradeTask)
                        await upgradeTask;
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
                        catch (Exception) {
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
                    File.Copy(GetDatabasePath(),
                        Path.Combine(Path.GetDirectoryName(GetDatabasePath())!, DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".bak"),
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

        [DbUpgrade(6)]
        private static async Task UpgradeTo6(LiteDatabase liteDatabase) {
            await Program.StartClient();
            await Program.WaitStartAsync;
            var guildConfigs = liteDatabase.GetCollection<GuildConfig>(@"Guilds");
            var temp = Program.Client.Guilds.Select(guild => (guild, guildConfigs.FindById(guild.Id))).ToList();
            guildConfigs.DeleteAll();
            foreach (var pair in temp) {
                pair.Item2.GuildId = pair.guild.Id;
                guildConfigs.Upsert(pair.Item2);
            }
        }

        [DbUpgrade(7)]
        private static async Task UpgradeTo7(LiteDatabase liteDatabase) {
            var messagesCollection = liteDatabase.GetCollection<MessageHistory>(@"MessagesHistory");
            var guildsCollection = liteDatabase.GetCollection<GuildConfig>(@"Guilds");
            messagesCollection.DeleteAll();
            await Program.StartClient();
            await Program.WaitStartAsync;
            var guilds = Program.Client.Guilds.Select(guild => (guild, guildsCollection.FindById((long) guild.Id)));
            foreach (var valueTuple in guilds.Where(tuple => tuple.Item2.IsLoggingEnabled)) {
                try {
                    await (await Program.Client.GetUser(valueTuple.guild.OwnerId).GetOrCreateDMChannelAsync()).SendMessageAsync(
                        "Message logging was enabled on your server. We reworked it, and now it works better.\n" +
                        $"Please **configure it** using the command `{valueTuple.Item2.Prefix}logging`");
                }
                catch (Exception) {
                    // ignored
                }
            }
        }

        [DbUpgrade(8)]
        private static Task UpgradeTo8(LiteDatabase liteDatabase) {
            var guildsCollection = liteDatabase.GetCollection(@"Guilds");
            var guildConfigs = guildsCollection.FindAll().ToList();
            guildsCollection.DeleteAll();
            foreach (var bsonDocument in guildConfigs) {
                var asDouble = bsonDocument["Volume"].AsDouble;
                bsonDocument["Volume"] = (int) (asDouble * 100);
            }

            guildsCollection.InsertBulk(guildConfigs);
            
            return Task.CompletedTask;
        }
        
        [DbUpgrade(9)]
        private static Task UpgradeTo9(LiteDatabase liteDatabase) {
            var guildsCollection = liteDatabase.GetCollection<GuildConfig>(@"Guilds");
            foreach (var guildConfig in guildsCollection.FindAll()) {
                guildConfig.Volume = Math.Min(100, guildConfig.Volume);
                guildsCollection.Upsert(guildConfig);
            }
            
            return Task.CompletedTask;
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