using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Bot.Commands;
using Discord;
using Newtonsoft.Json;

namespace Bot.Utilities
{
    class Localization
    {
        public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> Languages;
        public static Timer                                                              UpdateLanguageTimer;
        public static Dictionary<ulong, ServerLocalization>                              LocalizationOptions;

        public static void Initialize() {
            UpdateLanguageTimer = new Timer(state => Load(), null, 0, TimeSpan.FromMinutes(30).Milliseconds);
            LoadCache();
        }

        public static void Load() {
            Console.WriteLine("Start loading localizations packs...");
            try {
                Dictionary<string, Dictionary<string, Dictionary<string, string>>> temp = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
                foreach (var VARIABLE in
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(Utilities.DownloadString(@"https://gitlab.com/skprochlab/nJMaLBot/raw/master/nJMaLBot/Localization/Index.json"))) {
                    temp.Add(VARIABLE.Key, JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(Utilities.DownloadString(VARIABLE.Value)));
                }

                Languages = temp;
            }
            catch (Exception e) {
                Console.WriteLine($"Error while downloading: {e.Message}");
                Console.WriteLine("Loading default (en) pack.");
                Languages = new Dictionary<string, Dictionary<string, Dictionary<string, string>>> {
                                                                                                       {
                                                                                                           "en",
                                                                                                           JsonConvert
                                                                                                              .DeserializeObject<Dictionary<string, Dictionary<string, string>>
                                                                                                               >(File.ReadAllText("Localization/en.json"))
                                                                                                       }
                                                                                                   };
            }

            Console.WriteLine("End loading localization packs");
        }

        public static void LoadCache() {
            var path = Path.Combine("messageEditsLogs", "LanguageOptions.json");
            LocalizationOptions = File.Exists(path)
                                      ? JsonConvert.DeserializeObject<Dictionary<ulong, ServerLocalization>>(File.ReadAllText(path))
                                      : new Dictionary<ulong, ServerLocalization>();
        }

        public static void SaveCache() {
            var path = Path.Combine("messageEditsLogs", "LanguageOptions.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(LocalizationOptions));
        }

        public static string Get(string lang, string id) { return Get(lang, id.Split(".")[0], id.Split(".")[1]); }

        public static string Get(string lang, string group, string id) {
            try {
                return Languages[lang][group][id];
            }
            catch (Exception e) {
                return Languages["en"][group][id];
            }
        }

        public static string Get(ulong channelId, string group, string id) { return Get(GetLanguage(channelId), group, id); }
        public static string Get(ulong channelId, string id)                                 { return Get(GetLanguage(channelId),          id); }
        public static string Get(ulong guildId,   ulong  channelId, string group, string id) { return Get(GetLanguage(guildId, channelId), group, id); }
        public static string Get(ulong guildId,   ulong  channelId, string id) { return Get(GetLanguage(channelId), id); }

        public static string GetLanguage(ulong channelId) { return GetLanguage((Program.Client.GetChannel(channelId) as IGuildChannel).GuildId, channelId); }

        public static string GetLanguage(ulong guildId, ulong channelId) {
            if (!LocalizationOptions.ContainsKey(guildId)) {
                LocalizationOptions.Add(guildId, new ServerLocalization());

                EmbedBuilder eb = new EmbedBuilder();
                eb.WithFields(HelpUtils.BuildHelpField("setserverlanguage"))
                  .WithTitle(Get("en", "Help", "HelpMessage") + "`setserverlanguage`")
                  .WithColor(Color.Gold);
                Program.Client.GetGuild(guildId).DefaultChannel.SendMessageAsync(Get("en", "Localization", "LocalizationEmpty"), false, eb.Build());
            }
            else {
                if (!LocalizationOptions[guildId].Channels.ContainsKey(channelId))
                    LocalizationOptions[guildId].Channels.Add(channelId, LocalizationOptions[guildId].GuildLanguage);
            }

            return LocalizationOptions[guildId].Channels[channelId];
        }
    }

    public class ServerLocalization
    {
        public ulong                     GuildId       { get; set; }
        public string                    GuildLanguage { get; set; } = "en";
        public Dictionary<ulong, string> Channels      { get; set; } = new Dictionary<ulong, string>();
    }
}
