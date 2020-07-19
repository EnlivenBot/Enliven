using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NLog;

namespace Bot.Config.Localization {
    internal static class LocalizationManager {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static readonly Dictionary<string, LocalizationPack> Languages;
        static LocalizationManager() {
            logger.Info("Start loading localizations packs...");
            try {
                var indexes = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "Localization"))
                                       .ToDictionary(Path.GetFileNameWithoutExtension);
                logger.Info("Loaded languages: {lang}.", string.Join(", ", indexes.Select(pair => pair.Key)));
                Dictionary<string, LocalizationPack> localizationPacks = indexes.ToDictionary(variable => variable.Key,
                    variable => JsonConvert.DeserializeObject<LocalizationPack>(Utilities.Utilities.DownloadString(variable.Value)))!;

                var localizationEntries = localizationPacks["en"].Data.SelectMany(groups => groups.Value.Select(pair => groups.Key + pair.Key)).ToList();
                foreach (var pack in localizationPacks) {
                    var entriesNotLocalizedCount = pack.Value.Data.SelectMany(groups => groups.Value.Select(pair => groups.Key + pair.Key))
                                                       .Count(s => localizationEntries.Contains(s));

                    pack.Value.TranslationCompleteness = (int) (entriesNotLocalizedCount / (double)localizationEntries.Count * 100);
                }

                Languages = localizationPacks;
            }
            catch (Exception e) {
                logger.Error(e, "Error while downloading libraries");
                logger.Info("Loading default (en) pack.");
                Languages = new Dictionary<string, LocalizationPack> {
                    {
                        "en",
                        JsonConvert.DeserializeObject<LocalizationPack>(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Localization/en.json")))
                    }
                };
            }
            finally {
                logger.Info("End loading localization packs");
            }
        }

        public static void Initialize() {
            // Dummy method to call static constructor
        }

        public static string Get(string lang, string id) {
            var split = id.Split(".");

            return Get(lang, split[0], split[1]);
        }

        public static string Get(string lang, string group, string id) {
            logger.Trace("Requested {group}.{id} in {lang} localization", group, id, lang);
            if (Languages.TryGetValue(lang, out var pack) &&
                pack.Data.TryGetValue(group, out var reqGroup) &&
                reqGroup.TryGetValue(id, out var reqText)) {
                return reqText;
            }

            if (pack?.FallbackLanguage == null) {
                logger.Error(new Exception($"Failed to load {group}.{id} in en localization"), 
                    "Failed to load {group}.{id} in {lang} localization", group, id, "en");
                return $"{group}.{id}";
            }

            logger.Warn("Failed to load {group}.{id} in {lang} localization", group, id, lang);
            // ReSharper disable once TailRecursiveCall
            return Get(pack.FallbackLanguage, group, id);
        }

        public static string Get(ulong guildId, string id) {
            return Get(GuildConfig.Get(guildId).GetLanguage(), id);
        }

        public static string Get(GuildConfig guildConfig, string id) {
            return Get(guildConfig.GetLanguage(), id);
        }
    }
}