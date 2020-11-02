using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Common.Music;
using HarmonyLib;
using LiteDB;
using NLog;

// ReSharper disable UnusedMember.Local

#pragma warning disable 8632

namespace Common.Config {
    // ReSharper disable once InconsistentNaming
    public static class Database {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public static LiteDatabase LiteDatabase = null!;

        static Database() {
            InitializeDatabase();
            GlobalSettings = LiteDatabase.GetCollection<Entity>(@"Global");
            Guilds = LiteDatabase.GetCollection<GuildConfig>(@"Guilds");
            CommandStatistics = LiteDatabase.GetCollection<StatisticsPart>(@"CommandStatistics");
            Playlists = LiteDatabase.GetCollection<StoredPlaylist>(@"StoredPlaylists");
            Users = LiteDatabase.GetCollection<UserData>("UserData");
            
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
        public static readonly ILiteCollection<StatisticsPart> CommandStatistics;
        public static readonly ILiteCollection<StoredPlaylist> Playlists;
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

            LiteDatabase = LoadDatabase();
            LiteDatabase.CheckpointSize = 10000;

            PerformUpgrades().Wait();
            LiteDatabase.Checkpoint();

            _checkpointTimer = new Timer(state => LiteDatabase.Checkpoint(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));
            _rebuildTimer = new Timer(state => LiteDatabase.Rebuild(), null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(180));
            logger.Info("LiteDatabase loaded");
        }

        private static LiteDatabase LoadDatabase() {
            return new LiteDatabase(GetDatabasePath());
        }

        private static string GetDatabasePath() {
            return Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db");
        }

        private static async Task PerformUpgrades() {
            logger.Info("Looking for database upgrades");
            var liteDatabaseCheckpointSize = LiteDatabase.CheckpointSize;
            LiteDatabase.CheckpointSize = 0;
            var upgrades = Assembly.GetExecutingAssembly().GetTypes()
                                   .SelectMany(AccessTools.GetDeclaredMethods)
                                   .Where(m => m.GetCustomAttributes(typeof(DbUpgradeAttribute), false).Length > 0)
                                    // ReSharper disable once PossibleNullReferenceException
                                   .Select(info => ((DbUpgradeAttribute) info.GetCustomAttribute(typeof(DbUpgradeAttribute))!, info))
                                   .OrderBy(tuple => tuple.Item1.Version)
                                   .ToList();
            foreach (var upgrade in upgrades.SkipWhile((tuple, i) => tuple.Item1.Version <= LiteDatabase.UserVersion)) {
                logger.Info("Upgrading database to version {version}", upgrade.Item1.Version);

                if (upgrade.Item1.TransactionsFriendly) {
                    LiteDatabase.BeginTrans();
                }
                else {
                    logger.Info("Upgrade does not support transactions. We make a backup.");
                    LiteDatabase.Checkpoint();
                    LiteDatabase.Dispose();
                    File.Copy(GetDatabasePath(), Path.ChangeExtension(GetDatabasePath(), ".bak"), true);
                    logger.Info("Backup maked");
                    LiteDatabase = LoadDatabase();
                }

                try {
                    var upgradeResult = upgrade.info.Invoke(null, new object?[] {LiteDatabase});
                    if (upgradeResult is Task upgradeTask)
                        await upgradeTask;
                    if (upgrade.Item1.TransactionsFriendly) {
                        LiteDatabase.Commit();
                    }
                }
                catch (Exception e) {
                    logger.Fatal(e, "Error while upgrading database");
                    logger.Fatal("Rollbacking changes");
                    if (upgrade.Item1.TransactionsFriendly) {
                        LiteDatabase.Rollback();
                    }
                    else {
                        LiteDatabase.Checkpoint();
                        LiteDatabase.Dispose();
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
                    LiteDatabase.Checkpoint();
                    LiteDatabase.Rebuild();
                }
                catch (Exception e) {
                    // This is very bad
                    // We broke the database
                    logger.Fatal(e, "LiteDatabase file broken");
                    logger.Fatal("Doing backup");
                    File.Copy(GetDatabasePath(),
                        Path.Combine(Path.GetDirectoryName(GetDatabasePath())!, DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".bak"),
                        true);
                    logger.Fatal("Trying to copy intact information");
                    using (LiteDatabase newDb = new LiteDatabase("newDb.db")) {
                        foreach (var collectionName in LiteDatabase.GetCollectionNames()) {
                            try {
                                var collection = newDb.GetCollection(collectionName);
                                foreach (var bsonDocument in LiteDatabase.GetCollection(collectionName).FindAll()) {
                                    try {
                                        collection.Insert(bsonDocument);
                                    }
                                    catch (Exception exception) {
                                        logger.Error(exception, "LiteDatabase recreation error. Collection - {collectionName}", collectionName);
                                    }
                                }
                            }
                            catch (Exception exception) {
                                logger.Error(exception, "LiteDatabase recreation error. Collection - {collectionName}", collectionName);
                            }
                        }
                    }

                    LiteDatabase.Dispose();
                    File.Move("newDb.db", GetDatabasePath(), true);
                    LiteDatabase = LoadDatabase();
                }

                logger.Info("Checkpoint done");
                logger.Info("LiteDatabase upgraded to version {version}", upgrade.Item1.Version);
                LiteDatabase.UserVersion = upgrade.Item1.Version;
            }

            LiteDatabase.CheckpointSize = liteDatabaseCheckpointSize;
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