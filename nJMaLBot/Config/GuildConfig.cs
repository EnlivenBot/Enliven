using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Discord.WebSocket;
using LiteDB;

namespace Bot.Config {
    public class GuildConfig {
        [BsonId] public ulong GuildId { get; set; }
        public string Prefix { get; set; } = "&";
        public ConcurrentDictionary<ChannelFunction, ulong> FunctionalChannels { get; set; } = new ConcurrentDictionary<ChannelFunction, ulong>();
        public int Volume { get; set; }

        public GuildConfig SetChannel(string channelId, ChannelFunction func) {
            if (channelId == "null") {
                FunctionalChannels.TryRemove(func, out _);
            }
            else {
                FunctionalChannels[func] = Convert.ToUInt64(channelId);
            }
            return this;
        }

        public GuildConfig SetServerPrefix(string prefix) {
            Prefix = prefix;
            return this;
        }

        public bool GetChannel(ChannelFunction function, out SocketChannel channel) {
            if (FunctionalChannels.TryGetValue(function, out var value)) {
                channel = Program.Client.GetChannel(value);
                return true;
            }

            channel = null;
            return false;
        }

        public void Save() {
            GlobalDB.Guilds.Upsert(GuildId, this);
        }

        public static GuildConfig Get(ulong guildId) {
            return GlobalDB.Guilds.FindById(guildId) ?? new GuildConfig {GuildId = guildId};
        }
    }

    public enum ChannelFunction {
        Log,
        Music,
    }
}