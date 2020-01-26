using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Discord;
using Newtonsoft.Json;

namespace Bot.Config {
    internal static class Localization {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly Lazy<Dictionary<string, Dictionary<string, Dictionary<string, string>>>> _languages =
            new Lazy<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(LoadLanguages);

        public static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> Languages = _languages.Value;

        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadLanguages() {
            logger.Info("Start loading localizations packs...");
            try {
                var indexes = Directory.GetFiles("../../../Localization")
                                       .ToDictionary(Path.GetFileNameWithoutExtension);
                logger.Info("Loaded languages: {lang}.", string.Join(", ", indexes.Select(pair => pair.Key)));
                return
                    indexes.ToDictionary(variable => variable.Key,
                        variable =>
                            JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
                                Utilities.Utilities.DownloadString(variable.Value)));
            }
            catch (Exception e) {
                logger.Error(e, "Error while downloading libraries");
                logger.Info("Loading default (en) pack.");
                return new Dictionary<string, Dictionary<string, Dictionary<string, string>>> {
                    {
                        "en",
                        JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>
                            (File.ReadAllText("Localization/en.json"))
                    }
                };
            }
            finally {
                logger.Info("End loading localization packs");
            }
        }

        public static string Get(string lang, string id) {
            var split = id.Split(".");

            return Get(lang, split[0], split[1]);
        }

        public static string Get(string lang, string group, string id) {
            logger.Trace("Requested {group}.{id} in {lang} localization", group, id, lang);
            if (Languages.TryGetValue(lang, out var reqLang) &&
                reqLang.TryGetValue(group, out var reqGroup) &&
                reqGroup.TryGetValue(id, out var reqText)) {
                return reqText;
            }

            if (lang == "en") {
                logger.Error(new Exception($"Failed to load {group}.{id} in en localization"),"Failed to load {group}.{id} in {lang} localization", group, id, "en");
                return "";
            }

            logger.Warn("Failed to load {group}.{id} in {lang} localization", group, id, lang);
            // ReSharper disable once TailRecursiveCall
            return Get("en", group, id);
        }

        public static string Get(ulong guildId, string id) {
            return Get(GuildConfig.Get(guildId).GetLanguage(), id);
        }

        public static string Get(GuildConfig guildConfig, string id) {
            return Get(guildConfig.GetLanguage(), id);
        }
    }

    public interface ILocalizationProvider {
        string Get(string id);
        string Get(string group, string id);
    }

    public class LandLocalizationProvider : ILocalizationProvider {
        private readonly string _lang;
        public LandLocalizationProvider(string lang) {
            _lang = lang;
        }

        public string Get(string id) {
            return Localization.Get(_lang, id);
        }

        public string Get(string group, string id) {
            return Localization.Get(_lang, group, id);
        }
    }

    public class GuildLocalizationProvider : ILocalizationProvider {
        private readonly GuildConfig _guildConfig;

        public GuildLocalizationProvider(ulong guildId) {
            _guildConfig = GuildConfig.Get(guildId);
        }

        public GuildLocalizationProvider(GuildConfig guildConfig) {
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