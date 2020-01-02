using System;
using System.IO;
using LiteDB;

namespace Bot.Config {
    public class GlobalDB {
        public static LiteDatabase Database => _database.Value;
        private static readonly Lazy<LiteDatabase> _database = new Lazy<LiteDatabase>(Init);

        public static readonly LiteCollection<Entity> GlobalSettings = Database.GetCollection<Entity>(@"Global");
        public static readonly LiteCollection<Entity> Prefixes = Database.GetCollection<Entity>(@"Prefixes");


        private static LiteDatabase Init() {
            var tempdb = new LiteDatabase(@"DataBase.db");
            //Updating database in future
            tempdb.UserVersion = 1;
            return tempdb;
        }
    }
}