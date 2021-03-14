using Common.Config;
using LiteDB;

namespace Common {
    internal class DatabaseUpgrades {
        // Changes to the playlist storage system
        [LiteDatabaseProvider.DbUpgradeAttribute(10, false)]
        public static void Upgrade10(LiteDatabase database) {
            database.DropCollection("StoredPlaylists");
        }
    }
}