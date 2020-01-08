using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Bot.Config;
using Newtonsoft.Json;

namespace Bot.Utilities {
    internal class Localization {
        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> Languages;

        private static Timer _updateLanguageTimer = new Timer(state => LoadLanguages(), null, 0,
            TimeSpan.FromMinutes(30).Milliseconds);

        private static void LoadLanguages() {
            Console.WriteLine("Start loading localizations packs...");
            try {
                Languages = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                                            Utilities.DownloadString(@"https://gitlab.com/skprochlab/nJMaLBot/raw/master/nJMaLBot/Localization/Index.json"))
                                       .ToDictionary(variable => variable.Key,
                                            variable =>
                                                JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
                                                    Utilities.DownloadString(variable.Value)));
            }
            catch (Exception e) {
                Console.WriteLine($"Error while downloading: {e}");
                Console.WriteLine("Loading default (en) pack.");
                Languages = new Dictionary<string, Dictionary<string, Dictionary<string, string>>> {
                    {
                        "en",
                        JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>
                            (File.ReadAllText("Localization/en.json"))
                    }
                };
            }

            Console.WriteLine("End loading localization packs");
        }

        public static string Get(string lang, string id) {
            try {
                var split = id.Split(".");
                return Get(lang, split[0], split[1]);
            }
            catch (Exception) {
                return "";
            }
        }

        public static string Get(string lang, string group, string id) {
            try {
                return Languages[lang][group][id];
            }
            catch (Exception) {
                try {
                    return Languages["en"][group][id];
                }
                catch (Exception) {
                    return "";
                }
            }
        }

        public static string Get(ulong guildId, string id) {
            return Get(GuildConfig.Get(guildId).GetLanguage(), id);
        }
    }
}