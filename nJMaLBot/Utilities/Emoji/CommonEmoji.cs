using System;
using System.IO;
using System.Linq;
using Discord;
using Newtonsoft.Json;

namespace Bot.Utilities.Emoji {
    public static class CommonEmoji {
        public static Emote RepeatOnce { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOnce);
        public static Emote RepeatOff { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOff);
        public static Emote Repeat { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Repeat);
        public static Emote Play { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Play);
        public static Emote Pause { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Pause);
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
    }
}