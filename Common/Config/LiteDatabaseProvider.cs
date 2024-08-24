using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using NLog;

// ReSharper disable UnusedMember.Local

#pragma warning disable 8632

namespace Common.Config;

// ReSharper disable once InconsistentNaming
public class LiteDatabaseProvider
{
    // ReSharper disable once NotAccessedField.Local
    private static Timer _checkpointTimer = null!;

    // ReSharper disable once NotAccessedField.Local
    private static Timer _rebuildTimer = null!;
    private readonly Task<LiteDatabase> _databaseProvider;
    private readonly ILogger logger;

    static LiteDatabaseProvider()
    {
        //Fix for mapping TimeSpan?
        BsonMapper.Global.RegisterType
        (
            timeSpan => BsonMapper.Global.Serialize(timeSpan?.Ticks),
            // ReSharper disable once RedundantCast
            bson => bson == null ? (TimeSpan?)null : TimeSpan.FromTicks((long)bson.RawValue)
        );

        BsonMapper.Global.IncludeNonPublic = true;
        BsonMapper.Global.EmptyStringToNull = false;
    }

    public LiteDatabaseProvider(ILogger logger)
    {
        this.logger = logger;
        _databaseProvider = ProvideDatabaseInternal();
    }

    public Task<LiteDatabase> ProvideDatabase()
    {
        return _databaseProvider;
    }

    private async Task<LiteDatabase> ProvideDatabaseInternal()
    {
        logger.Info("Loading database");

        var db = LoadDatabase();
        db.CheckpointSize = 10000;

        db = await PerformUpgrades(db);
        db.Checkpoint();

        _checkpointTimer = new Timer(state => db.Checkpoint(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));
        _rebuildTimer = new Timer(state => db.Rebuild(), null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(180));
        logger.Info("LiteDatabase loaded");

        return db;
    }

    private static LiteDatabase LoadDatabase()
    {
        return new LiteDatabase(GetDatabasePath());
    }

    private static string GetDatabasePath()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "Config", @"DataBase.db");
    }

    private async Task<LiteDatabase> PerformUpgrades(LiteDatabase db)
    {
        logger.Info("Looking for database upgrades");
        var liteDatabaseCheckpointSize = db.CheckpointSize;
        db.CheckpointSize = 0;
        var upgrades = Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(t => t.GetMethods())
            .Select(info => (info.GetCustomAttribute<DbUpgradeAttribute>(), info))
            .Where(tuple => tuple.Item1 != null)
            .OrderBy(tuple => tuple.Item1!.Version)
            .ToList();
        foreach (var (dbUpgradeAttribute, method) in upgrades.SkipWhile((tuple, i) =>
                     tuple.Item1!.Version <= db.UserVersion))
        {
            logger.Info("Upgrading database to version {version}", dbUpgradeAttribute!.Version);

            if (dbUpgradeAttribute.TransactionsFriendly)
            {
                db.BeginTrans();
            }
            else
            {
                logger.Info("Upgrade does not support transactions. We make a backup.");
                db.Checkpoint();
                db.Dispose();
                File.Copy(GetDatabasePath(), Path.ChangeExtension(GetDatabasePath(), ".bak"), true);
                logger.Info("Backup maked");
                db = LoadDatabase();
            }

            try
            {
                var upgradeResult = method.Invoke(null, new object?[] { db });
                if (upgradeResult is Task upgradeTask)
                    await upgradeTask;
                if (dbUpgradeAttribute.TransactionsFriendly)
                {
                    db.Commit();
                }
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Error while upgrading database");
                logger.Fatal("Rollbacking changes");
                if (dbUpgradeAttribute.TransactionsFriendly)
                {
                    db.Rollback();
                }
                else
                {
                    db.Checkpoint();
                    db.Dispose();
                    File.Copy(Path.ChangeExtension(GetDatabasePath(), ".bak"), GetDatabasePath(), true);
                }

                throw;
            }
            finally
            {
                if (!dbUpgradeAttribute.TransactionsFriendly)
                {
                    try
                    {
                        File.Delete(Path.ChangeExtension(GetDatabasePath(), ".bak"));
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }

            logger.Info("Making a checkpoint");
            try
            {
                db.Checkpoint();
                db.Rebuild();
            }
            catch (Exception e)
            {
                // This is very bad
                // We broke the database
                logger.Fatal(e, "LiteDatabase file broken");
                logger.Fatal("Doing backup");
                File.Copy(GetDatabasePath(),
                    Path.Combine(Path.GetDirectoryName(GetDatabasePath())!,
                        DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".bak"),
                    true);
                logger.Fatal("Trying to copy intact information");
                using (LiteDatabase newDb = new LiteDatabase("newDb.db"))
                {
                    foreach (var collectionName in db.GetCollectionNames())
                    {
                        try
                        {
                            var collection = newDb.GetCollection(collectionName);
                            foreach (var bsonDocument in db.GetCollection(collectionName).FindAll())
                            {
                                try
                                {
                                    collection.Insert(bsonDocument);
                                }
                                catch (Exception exception)
                                {
                                    logger.Error(exception,
                                        "LiteDatabase recreation error. Collection - {collectionName}", collectionName);
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            logger.Error(exception, "LiteDatabase recreation error. Collection - {collectionName}",
                                collectionName);
                        }
                    }
                }

                db.Dispose();
                File.Move("newDb.db", GetDatabasePath(), true);
                db = LoadDatabase();
            }

            logger.Info("Checkpoint done");
            logger.Info("LiteDatabase upgraded to version {version}", dbUpgradeAttribute.Version);
            db.UserVersion = dbUpgradeAttribute.Version;
        }

        db.CheckpointSize = liteDatabaseCheckpointSize;

        return db;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class DbUpgradeAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        public DbUpgradeAttribute(int version, bool transactionsFriendly = true)
        {
            Version = version;
            TransactionsFriendly = transactionsFriendly;
        }

        public int Version { get; }
        public bool TransactionsFriendly { get; }
    }
}