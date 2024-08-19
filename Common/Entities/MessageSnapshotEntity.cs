using System;
using LiteDB;

namespace Common.Entities;

internal class MessageSnapshotEntity
{
    public DateTimeOffset EditTimestamp { get; set; }

    [BsonField("Value")] public string DiffString { get; set; } = null!;
}