using System;
using System.Collections.Concurrent;
using Bot.Commands;
using Bot.Utilities;
using Discord;
using Discord.WebSocket;
using LiteDB;

namespace Bot.Config {
    public partial class GuildConfig {
        [BsonId] public ulong GuildId { get; set; }
        public string Prefix { get; set; } = "&";
        public int Volume { get; set; } = 100;
        public ConcurrentDictionary<ChannelFunction, ulong> FunctionalChannels { get; set; } = new ConcurrentDictionary<ChannelFunction, ulong>();
        public string GuildLanguage { get; set; }

        public void Save() {
            GlobalDB.Guilds.Upsert(GuildId, this);
        }

        public static GuildConfig Get(ulong guildId) {
            return GlobalDB.Guilds.FindById(guildId) ?? new GuildConfig {GuildId = guildId};
        }
    }

    public enum ChannelFunction {
        Log,
        Music
    }

    public partial class GuildConfig {
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
                eb.WithFields(HelpUtils.BuildHelpField("setlanguage"))
                  .WithTitle(Localization.Get("en", "Help", "HelpMessage") + "`setlanguage`")
                  .WithColor(Color.Gold);
                Program.Client.GetGuild(GuildId).DefaultChannel
                       .SendMessageAsync(Localization.Get("en", "Localization", "LocalizationEmpty"), false, eb.Build());
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
            return this;
        }
    }
}