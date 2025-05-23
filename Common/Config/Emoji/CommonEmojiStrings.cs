﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Common.Config.Emoji;

public class CommonEmojiStrings
{
    private static Lazy<CommonEmojiStrings> _lazy = new(
        () =>
        {
            var emojiStrings = new CommonEmojiStrings();
            var path = Path.Combine("Config", "CommonEmoji.json");
            if (File.Exists(path))
                emojiStrings = JsonConvert.DeserializeObject<CommonEmojiStrings>(File.ReadAllText(path))!;
            File.WriteAllText(path, JsonConvert.SerializeObject(emojiStrings, Formatting.Indented));
            return emojiStrings;
        });

    private static Dictionary<string, string> _getEmojiCache = new();

    private CommonEmojiStrings()
    {
    }

    public static CommonEmojiStrings Instance => _lazy.Value;

    public string RepeatOne { get; set; } = "<:repeatone:1030612485914497126>";
    public string RepeatOff { get; set; } = "<:repeatoff:1030612482433241128>";
    public string Repeat { get; set; } = "<:repeat:1030610163670982736>";
    public string Play { get; set; } = "<:play:682580118358458368>";
    public string Pause { get; set; } = "<:pause:682580118425960469>";
    public string Stop { get; set; } = "<:stop:682658172615524382>";
    public string Spotify { get; set; } = "<:spotify:764837934519156746>";
    public string YandexMusic { get; set; } = "<:yandexmusic:1112134818839408754>";
    public string VkMusic { get; set; } = "<:vkmusic:1112149911711010887>";
    public string RepeatBox { get; set; } = "<:repeatbox:1030612480696791040>";
    public string RepeatOffBox { get; set; } = "<:repeatoffbox:1030612484094181396>";
    public string RepeatOneBox { get; set; } = "<:repeatonebox:1030612487344763070>";
    public string LegacyTrackNext { get; set; } = "⏭️";
    public string LegacyTrackPrevious { get; set; } = "⏮️";
    public string LegacyPause { get; set; } = "⏸️";
    public string LegacyPlay { get; set; } = "▶️";
    public string LegacyStop { get; set; } = "⏹️";
    public string LegacySound { get; set; } = "🔉";
    public string LegacyLoudSound { get; set; } = "🔊";
    public string LegacyRepeat { get; set; } = "🔁";
    public string LegacyShuffle { get; set; } = "🔀";
    public string LegacyBook { get; set; } = "📖";
    public string LegacyPlayPause { get; set; } = "⏯️";
    public string LegacyArrowDown { get; set; } = "⬇️";
    public string LegacyFileBox { get; set; } = "🗃️";
    public string LegacyReverse { get; set; } = "◀️";
    public string LegacyFastReverse { get; set; } = "⏪";
    public string LegacyFastForward { get; set; } = "⏩";
    public string Help { get; set; } = "ℹ️";
    public string Memo { get; set; } = "📝";
    public string Robot { get; set; } = "🤖";
    public string ExclamationPoint { get; set; } = "⁉️";
    public string Printer { get; set; } = "🖨️";
    public string InputNumbers { get; set; } = "🔢";
    public string ThumbsUp { get; set; } = "👍";
    public string ThumbsDown { get; set; } = "👎";
    public string Warning { get; set; } = "⚠️";
    public string BookmarkTabs { get; set; } = "📑";
    public string NoEntry { get; set; } = "⛔";
    public string Level { get; set; } = "🎚️";
    public string E { get; set; } = "🇪";

    // Animated

    // https://cdn.discordapp.com/emojis/961698515694805022.gif?quality=lossless
    public string LoadingAnimated { get; set; } = "<a:loading:961698515694805022>";

    public string GetEmoji(string name)
    {
        if (_getEmojiCache.TryGetValue(name, out var emojiString)) return emojiString;
        emojiString = GetType().GetProperty(name)?.GetValue(this)?.ToString()
                      ?? throw new ArgumentException("No emoji with this name found");
        _getEmojiCache[name] = emojiString;
        return emojiString;
    }
}