using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Discord;
using Newtonsoft.Json;

namespace SKProCH_s_Discord_Bot
{
    class Utilities
    { }

    public static class ChannelUtils
    {
        public enum ChannelFunction
        {
            Log, Music
        }

        private static ConcurrentDictionary<ulong, ConcurrentDictionary<ChannelFunction, ulong>> FunctionsChannel;

        public static void LoadCache() {
            var path = Path.Combine("messageEditsLogs", "FunctionChannels.json");
            FunctionsChannel = File.Exists(path)
                                   ? JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, ConcurrentDictionary<ChannelFunction, ulong>>>(File.ReadAllText(path))
                                   : new ConcurrentDictionary<ulong, ConcurrentDictionary<ChannelFunction, ulong>>();
        }

        public static void SaveCache() {
            var path = Path.Combine("messageEditsLogs", "FunctionChannels.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(FunctionsChannel));
        }

        public static void SetChannel(ulong guildId, ulong channelId, ChannelFunction func) {
            if (!FunctionsChannel.ContainsKey(guildId))
                FunctionsChannel.TryAdd(guildId, new ConcurrentDictionary<ChannelFunction, ulong>());
            if (!FunctionsChannel[guildId].TryAdd(func, channelId)) {
                ulong justforget = 0;
                FunctionsChannel[guildId].TryRemove(func, out justforget);
                FunctionsChannel[guildId].TryAdd(func, channelId);
            }

            SaveCache();
        }

        public static ulong GetChannel(ulong guild, ChannelFunction func) {
            FunctionsChannel.TryGetValue(guild, out var concurrentDictionaryElement);
            if (concurrentDictionaryElement == null)
                throw new NoSuchChannelException("Для этого сервера нет назначенных каналов");
            concurrentDictionaryElement.TryGetValue(func, out var toreturn);
            if (toreturn == 0) {
                throw new NoSuchChannelException($"Для этого сервера не назначен канал, выполняющий функцию `{func.ToString()}`");
            }
            return toreturn;
        }
    }

    class NoSuchChannelException : Exception
    {
        public NoSuchChannelException() {}
        public NoSuchChannelException(string message) : base(message){}
        public NoSuchChannelException(string message, Exception inner) : base(message, inner) { }
    }
}
