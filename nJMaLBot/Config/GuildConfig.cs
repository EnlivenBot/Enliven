using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Bot.Config.Localization.Providers;
using Discord.WebSocket;
using LiteDB;

namespace Bot.Config {
    public partial class GuildConfig {
        private ILocalizationProvider _loc = null!;
        [BsonId] public ulong GuildId { get; set; }
        public string Prefix { get; set; } = "&";
        public float Volume { get; set; } = 1f;
        public ConcurrentDictionary<ChannelFunction, ulong> FunctionalChannels { get; set; } = new ConcurrentDictionary<ChannelFunction, ulong>();
        public string GuildLanguage { get; set; } = "en";
        public bool IsMusicLimited { get; set; }

        public bool IsLoggingEnabled { get; set; } = false;
        public bool IsCommandLoggingEnabled { get; set; } = false;
        public List<ulong> LoggedChannels { get; set; } = new List<ulong>();

        [BsonIgnore] public ILocalizationProvider Loc => _loc ??= new GuildLocalizationProvider(this);
    }

    public enum ChannelFunction {
        Log,
        Music
    }

    public partial class GuildConfig {
        private static ConcurrentDictionary<ulong, GuildConfig> _configCache = new ConcurrentDictionary<ulong, GuildConfig>();
        public static GuildConfig Get(ulong guildId) {
            return _configCache.GetOrAdd(guildId, arg => {
                var guildConfig = GlobalDB.Guilds.FindById(arg);
                return guildConfig ?? TryCreate(guildId, true);
            });
        }
        
        public void Save() {
            GlobalDB.Guilds.Upsert(GuildId, this);
        }

        public static event EventHandler<string> LocalizationChanged = null!;
        public GuildConfig SetChannel(string channelId, ChannelFunction func) {
            if (channelId == "null")
                FunctionalChannels.TryRemove(func, out _);
            else
                FunctionalChannels[func] = Convert.ToUInt64(channelId);

            if (func == ChannelFunction.Log) {
                LoggedChannels.Remove(Convert.ToUInt64(channelId));
            }

            return this;
        }

        public bool GetChannel(ChannelFunction function, out SocketChannel? channel) {
            if (FunctionalChannels.TryGetValue(function, out var value)) {
                channel = Program.Client.GetChannel(value);
                return true;
            }

            channel = null;
            return false;
        }

        public string GetLanguage() {
            return GuildLanguage;
        }

        public GuildConfig SetLanguage(string language) {
            GuildLanguage = language;
            LocalizationChanged?.Invoke(this, language);
            return this;
        }

        public static GuildConfig TryCreate(ulong guildId, bool displayWelcomeMessage) {
            var guildConfig = GlobalDB.Guilds.FindById(guildId);
            if (guildConfig != null) {
                return guildConfig;
            }
            guildConfig = new GuildConfig {GuildId = guildId};
            guildConfig.Save();
            if (displayWelcomeMessage) {
                #pragma warning disable 4014
                GlobalBehaviors.PrintWelcomeMessage(Program.Client.GetGuild(guildId));
                #pragma warning restore 4014
            }

            return guildConfig;
        }
    }
}