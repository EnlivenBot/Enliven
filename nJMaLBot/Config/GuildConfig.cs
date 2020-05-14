using System;
using System.Collections.Concurrent;
using Bot.Config.Localization.Providers;
using Bot.Utilities.Commands;
using Discord;
using Discord.WebSocket;
using LiteDB;

namespace Bot.Config {
    public partial class GuildConfig {
        [BsonId] public ulong GuildId { get; set; }
        public string Prefix { get; set; } = "&";
        public float Volume { get; set; } = 1f;
        public ConcurrentDictionary<ChannelFunction, ulong> FunctionalChannels { get; set; } = new ConcurrentDictionary<ChannelFunction, ulong>();
        public string GuildLanguage { get; set; }
        public bool IsMusicLimited { get; set; }

        public bool IsLoggingEnabled { get; set; } = false;
        public bool IsCommandLoggingEnabled { get; set; } = false;
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
                if (guildConfig != null) return guildConfig;

                guildConfig = new GuildConfig {GuildId = arg};
                guildConfig.Save();

                return guildConfig;
            });
        }
        
        public void Save() {
            GlobalDB.Guilds.Upsert(GuildId, this);
        }

        public static event EventHandler<string> LocalizationChanged;
        public GuildConfig SetChannel(string channelId, ChannelFunction func) {
            if (channelId == "null")
                FunctionalChannels.TryRemove(func, out _);
            else
                FunctionalChannels[func] = Convert.ToUInt64(channelId);

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

        public string GetLanguage() {
            if (!string.IsNullOrWhiteSpace(GuildLanguage)) return GuildLanguage;
            try {
                var eb = new EmbedBuilder();
                eb.WithFields(HelpUtils.BuildHelpFields("setlanguage", Prefix, new LangLocalizationProvider("en")))
                  .WithTitle(Localization.Localization.Get("en", "Help", "HelpMessage") + "`setlanguage`")
                  .WithColor(Color.Gold);
                Program.Client.GetGuild(GuildId).DefaultChannel
                       .SendMessageAsync(Localization.Localization.Get("en", "Localization", "LocalizationEmpty"), false, eb.Build());
            }
            catch (Exception) {
                // ignored
            }
            finally {
                GuildLanguage = "en";
                Save();
            }

            return GuildLanguage;
        }

        public GuildConfig SetLanguage(string language) {
            GuildLanguage = language;
            LocalizationChanged?.Invoke(this, language);
            return this;
        }
        
        public GuildConfig SetVolume(float volume) {
            Volume = volume;
            return this;
        }
    }
}