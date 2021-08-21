using System;
using System.Collections.Generic;
using System.Linq;
using Common.Config;
using Common.Entities;
using LiteDB;
using NLog;
#pragma warning disable 618

namespace Common {
    internal class DatabaseUpgrades {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        // Changes to the playlist storage system
        [LiteDatabaseProvider.DbUpgradeAttribute(10, false)]
        public static void Upgrade10(LiteDatabase database) {
            database.DropCollection("StoredPlaylists");
        }
        
        // Changes to the message history storage system
        [LiteDatabaseProvider.DbUpgradeAttribute(11, false)]
        public static void Upgrade11(LiteDatabase database) {
            var liteCollection = database.GetCollection("MessageHistory");
            var liteCollection2 = database.GetCollection("MessagesHistory");

            var enumerable = liteCollection.FindAll().Concat(liteCollection2.FindAll());
            var messageHistories = new List<MessageHistory>();
            foreach (var doc in enumerable) {
                try {
                    var attachments = doc.TryGetValue("At", out var at) ? at : null;
                    var isHistoryUnavailable = doc.TryGetValue("U", out var u) ? u : null;
                    var authorId = doc.TryGetValue("A", out var a) ? a : null;
                    var edits = doc.TryGetValue("E", out var e) ? e : null;
                    var id = doc.TryGetValue("_id", out var i) ? i : null;

                    var messageHistory = new MessageHistory() {
                        Id = id!.AsString,
                        Author = new UserLink((ulong) authorId!.AsInt64),
                        IsHistoryUnavailable = isHistoryUnavailable!.AsBoolean,
                        Attachments = attachments?.AsArray?.Select(value => value.AsString).ToList() ?? new List<string>(),
                    };
                    if (edits is not null)
                        foreach (var bsonValue in edits.AsArray) {
                            var editTimestamp = bsonValue.AsDocument.TryGetValue("EditTimestamp", out var et) ? et : null;
                            var value = bsonValue.AsDocument.TryGetValue("Value", out var v) ? v : null;
                            messageHistory.AddSnapshotInternal(editTimestamp!.AsDateTime, value!.AsString);
                        }
                
                    messageHistories.Add(messageHistory);
                }
                catch (Exception e) {
                    _logger.Error(e, "Error while parsing MessageHistory. Data: {Document}", doc.ToString());
                }
            }

            database.DropCollection("MessageHistory");
            database.DropCollection("MessagesHistory");
            database.Rebuild();

            var collection = database.GetCollection<MessageHistory>("MessageHistory");
            collection.InsertBulk(messageHistories);
        }
    }
}