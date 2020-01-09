using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Bot.Config {
    internal static class Localization {
        private static readonly Lazy<Dictionary<string, Dictionary<string, Dictionary<string, string>>>> _languages =
            new Lazy<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(LoadLanguages);

        public static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> Languages = _languages.Value;

        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadLanguages() {
            Console.WriteLine("Start loading localizations packs...");
            try {
                #if DEBUG
                var indexes = Directory.GetFiles("../../../Localization")
                                       .ToDictionary(Path.GetFileNameWithoutExtension)
                                       .Where(pair => pair.Key != "index");
                #endif
                #if !DEBUG
                var indexes = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                                    Utilities.Utilities.DownloadString(@"https://gitlab.com/skprochlab/nJMaLBot/raw/master/nJMaLBot/Localization/Index.json"));
                #endif
                return
                    indexes.ToDictionary(variable => variable.Key,
                        variable =>
                            JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
                                Utilities.Utilities.DownloadString(variable.Value)));
            }
            catch (Exception e) {
                Console.WriteLine($"Error while downloading: {e}");
                Console.WriteLine("Loading default (en) pack.");
                return new Dictionary<string, Dictionary<string, Dictionary<string, string>>> {
                    {
                        "en",
                        JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>
                            (File.ReadAllText("Localization/en.json"))
                    }
                };
            }
            finally {
                Console.WriteLine("End loading localization packs");
            }
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

    public class LocalizationProvider {
        private readonly GuildConfig _guildConfig;

        public LocalizationProvider(ulong guildId) {
            _guildConfig = GuildConfig.Get(guildId);
        }

        public LocalizationProvider(GuildConfig guildConfig) {
            _guildConfig = guildConfig;
        }

        public string Get(string id) {
            return Localization.Get(_guildConfig.GetLanguage(), id);
        }

        public string Get(string group, string id) {
            return Localization.Get(_guildConfig.GetLanguage(), group, id);
        }
    }
}