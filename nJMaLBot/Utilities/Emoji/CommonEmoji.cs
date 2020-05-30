using System;
using System.IO;
using Discord;
using Newtonsoft.Json;

namespace Bot.Utilities.Emoji {
    public static class CommonEmoji {
        public static Emote RepeatOnce { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOnce);
        public static Emote RepeatOff { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOff);
        public static Emote Repeat { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Repeat);
        public static Emote Play { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Play);
        public static Emote Pause { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Pause);
        public static Emote Stop { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Stop);
        public static Discord.Emoji LegacyTrackNext { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyTrackNext);
        public static Discord.Emoji LegacyTrackPrevious { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyTrackPrevious);
        public static Discord.Emoji LegacyPause { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyPause);
        public static Discord.Emoji LegacyPlay { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyPlay);
        public static Discord.Emoji LegacyStop { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyStop);
        public static Discord.Emoji LegacySound { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacySound);
        public static Discord.Emoji LegacyLoudSound { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyLoudSound);
        public static Discord.Emoji LegacyRepeat { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyRepeat);
        public static Discord.Emoji LegacyShuffle { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyShuffle);
        public static Discord.Emoji LegacyBook { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyBook);
        public static Discord.Emoji LegacyPlayPause { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyPlayPause);
        public static Discord.Emoji LegacyArrowDown { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyArrowDown);
        public static Discord.Emoji LegacyFileBox { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyFileBox);
        public static Discord.Emoji LegacyReverse { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.LegacyReverse);
        public static Discord.Emoji Help { get; set; } = new Discord.Emoji(CommonEmojiStrings.Instance.Help);
    }

    public class CommonEmojiStrings {
        private CommonEmojiStrings() { }

        private static Lazy<CommonEmojiStrings> _lazy = new Lazy<CommonEmojiStrings>(
            () => {
                var emojiStrings = new CommonEmojiStrings();
                if (File.Exists(Path.Combine("Config", "CommonEmoji.json")))
                    emojiStrings = JsonConvert.DeserializeObject<CommonEmojiStrings>(File.ReadAllText(Path.Combine("Config", "CommonEmoji.json")));
                File.WriteAllText(Path.Combine("Config", "CommonEmoji.json"), JsonConvert.SerializeObject(emojiStrings, Formatting.Indented));
                return emojiStrings;
            });

        public static CommonEmojiStrings Instance => _lazy.Value;

        public string RepeatOnce { get; set; } = "<:repeatonce:682469899351621648>";
        public string RepeatOff { get; set; } = "<:repeatoff:682469899276517401>";
        public string Repeat { get; set; } = "<:repeat:682469899066409043>";
        public string Play { get; set; } = "<:play:682580118358458368>";
        public string Pause { get; set; } = "<:pause:682580118425960469>";
        public string Stop { get; set; } = "<:stop:682658172615524382>";
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
        public string Help { get; set; } = "ℹ️";
    }
}