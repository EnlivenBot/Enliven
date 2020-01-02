using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using Bot.Config;
using Discord;
using Newtonsoft.Json;

namespace Bot.Utilities
{
    class Utilities
    {
        public static string DownloadFile(string url, string path) {
            using (WebClient wc = new WebClient()) {
                wc.DownloadFile(url, path);
            }

            return path;
        }

        public static string DownloadString(string url) {
            using WebClient wc = new WebClient();
            return wc.DownloadString(url);
        }
    }

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
                FunctionsChannel[guildId].TryRemove(func, out _);
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

        public static bool IsFuncChannel(ulong guildId, ulong channelId, ChannelFunction func) {
            try {
                FunctionsChannel.TryGetValue(guildId, out var concurrentDictionaryElement);
                concurrentDictionaryElement.TryGetValue(func, out var toreturn);
                return toreturn == channelId;
            }
            catch (Exception) {
                return false;
            }
        }

        public static bool IsFuncChannel(ulong channelId, ChannelFunction func) { return IsFuncChannel(((IGuildChannel) Program.Client.GetChannel(channelId)).GuildId, channelId, func); }

        public static bool IsChannelAssigned(ulong guildId, ChannelFunction func) {
            try {
                FunctionsChannel.TryGetValue(guildId, out var concurrentDictionaryElement);
                return concurrentDictionaryElement.ContainsKey(func);
            }
            catch (Exception) {
                return false;
            }
        }

        public static bool IsChannelAssigned(ulong guildId, ChannelFunction func, out ulong channelId) {
            try {
                FunctionsChannel.TryGetValue(guildId, out var concurrentDictionaryElement);
                if (concurrentDictionaryElement.ContainsKey(func)) {
                    channelId = concurrentDictionaryElement[func];
                    return true;
                }
            }
            catch (Exception) {
                // ignored
            }

            channelId = 0;
            return false;
        }
    }

    class NoSuchChannelException : Exception
    {
        public NoSuchChannelException() { }
        public NoSuchChannelException(string message) : base(message) { }
        public NoSuchChannelException(string message, Exception inner) : base(message, inner) { }
    }

    public static class ServerUtils {
        public static string GetServerPrefix(ulong serverId) {
            return GlobalDB.Prefixes.FindById(serverId.ToString()) ?? "&";
        }
        
        public static void SetServerPrefix(ulong serverId, string prefix) {
            GlobalDB.Prefixes.Upsert(serverId.ToString(), new Entity(prefix));
        }
    }
}
