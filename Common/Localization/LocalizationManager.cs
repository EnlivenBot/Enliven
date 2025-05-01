using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Common.Config.Emoji;
using Common.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Common.Localization;

public static class LocalizationManager
{
    private static readonly ILogger Logger = StaticLogger.Create(nameof(LocalizationManager));
    private static readonly Regex LocLayoutRegex = new(@"{loc:(\w+)\.(\w+)}");
    private static readonly Regex EmojiLayoutRegex = new(@"{emoji:(\w+)}");

    public static readonly Dictionary<string, LocalizationPack> Languages;

    static LocalizationManager()
    {
        Logger.LogInformation("Start loading localizations packs...");
        try
        {
            var localizationDirs = Path.Combine(Directory.GetCurrentDirectory(), "Localization");
            var indexes = Directory.GetFiles(localizationDirs)
                .ToDictionary<string, string>(Path.GetFileNameWithoutExtension);
            var localizationPacks = FetchLocalizationPacks(indexes!);

            var englishLocalizationEntries = localizationPacks["en"].GetLocalizationEntries();
            foreach (var pack in localizationPacks)
            {
                pack.Value.CalcTranslationCompleteness(englishLocalizationEntries);
            }

            Languages = localizationPacks!;
            Logger.LogInformation("Loaded languages: {Lang}",
                string.Join(", ", Languages.Select(pair => $"{pair.Key} - {pair.Value.TranslationCompleteness}%")));
        }
        catch (Exception e)
        {
            Logger.LogDebug(e, "Error while downloading languages");
            Logger.LogInformation("Loading default (en) pack");
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Localization/en.json");
            Languages = new Dictionary<string, LocalizationPack>
            {
                {
                    "en",
                    JsonConvert.DeserializeObject<LocalizationPack>(File.ReadAllText(path))!
                }
            };
        }
        finally
        {
            Logger.LogInformation("End loading localization packs");
        }
    }

    private static Dictionary<string, LocalizationPack> FetchLocalizationPacks(Dictionary<string, string> indexes)
    {
        return indexes.Select(Loader)
            .Where(pair => pair.HasValue)
            .Select(tuple => tuple!.Value)
            .ToDictionary(tuple => tuple.Key, tuple => tuple.Pack);

        static (string Key, LocalizationPack Pack)? Loader(KeyValuePair<string, string> variable)
        {
            var (packId, path) = variable;
            try
            {
                return (Key: packId, Pack: LoadLocalizationPack(path));
            }
            catch (Exception e)
            {
                Logger.LogDebug(e, "Error while loading {PackId} from {Path}", packId, path);
            }

            return null;
        }
    }

    private static LocalizationPack LoadLocalizationPack(string path)
    {
        var text = File.ReadAllText(path);
        var pack = JsonConvert.DeserializeObject<LocalizationPack>(text)!;
        foreach (var (_, dictionary) in pack.Data)
        {
            foreach (var (key, value) in dictionary.ToImmutableDictionary())
            {
                dictionary[key] = ProcessLayouts(value, pack);
            }
        }

        return pack;

        static string ProcessLayouts(string text, LocalizationPack localizationPack)
        {
            string startText;
            do
            {
                startText = text;
                text = ProcessLayout(LocLayoutRegex, text,
                    s => localizationPack.TryGetEntry(s[0], s[1], out var value)
                        ? value
                        : throw new ArgumentException($"Locale entry {s[0]}.{s[1]} does not exists"));
                text = ProcessLayout(EmojiLayoutRegex, text,
                    values => CommonEmojiStrings.Instance.GetEmoji(values[0]));
            } while (startText != text);

            return text;
        }

        static string ProcessLayout(Regex layoutRegex, string text, Func<List<string>, string> replace)
        {
            foreach (Match? match in layoutRegex.Matches(text))
            {
                text = layoutRegex.Replace(text,
                    replace(match!.Groups.Values.Skip(1).Select(group => group.Value).ToList()), 1);
            }

            return text;
        }
    }

    public static void Initialize()
    {
        // Dummy method to call static constructor
    }

    public static bool IsLocalizationExists(string groupId)
    {
        var split = groupId.Split(".");
        // English always contains more than other languages
        return Languages.TryGetValue("en", out var pack)
               && pack.Data.TryGetValue(split[0], out var reqGroup)
               && reqGroup.ContainsKey(split[1]);
    }

    public static string Get(string lang, string id, params object[]? args)
    {
        var split = id.Split(".");

        var s = Get(lang, split[0], split[1]);
        try
        {
            return args == null || args.Length == 0 ? s : string.Format(s, args);
        }
        catch (Exception e)
        {
            Logger.LogDebug(e, "Failed to format localization");
            return s;
        }
    }

    public static string Get(string lang, string group, string id)
    {
        Logger.LogTrace("Requested {Group}.{Id} in {Lang} localization", group, id, lang);
        if (Languages.TryGetValue(lang, out var pack) && pack.TryGetEntry(group, id, out var reqText))
        {
            return reqText;
        }

        if (string.IsNullOrWhiteSpace(pack?.FallbackLanguage))
        {
            Logger.LogDebug(new Exception($"Failed to load {group}.{id} in en localization"),
                "Failed to load {Group}.{Id} in {Lang} localization", group, id, "en");
            return $"{group}.{id}";
        }

        Logger.LogWarning("Failed to load {Group}.{Id} in {Lang} localization", group, id, lang);
        // ReSharper disable once TailRecursiveCall
        return Get(pack.FallbackLanguage, group, id);
    }
}