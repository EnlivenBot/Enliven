using Common.Config;
using LiteDB;
using NLog;

#pragma warning disable 618

namespace Common;

internal class DatabaseUpgrades
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    // Changes to the playlist storage system
    [LiteDatabaseProvider.DbUpgrade(10, false)]
    public static void Upgrade10(LiteDatabase database)
    {
        database.DropCollection("StoredPlaylists");
    }

    // Changes to the playlist storage system
    [LiteDatabaseProvider.DbUpgrade(12, false)]
    public static void Upgrade12(LiteDatabase database)
    {
        database.DropCollection("StoredPlaylists");
    }

    // Changes to the message history storage system
    [LiteDatabaseProvider.DbUpgrade(13, false)]
    public static void Upgrade11(LiteDatabase database)
    {
        database.DropCollection("MessageHistory");
        database.DropCollection("MessagesHistory");
    }
}