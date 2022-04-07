using System;
using System.IO;
using Newtonsoft.Json;

namespace Common.Config.Emoji {
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

        public string GetEmoji(string name) {
            return this.GetType().GetField("name")?.GetValue(this)?.ToString() ?? throw new ArgumentException("No emoji with this name found"); 
        }

        public string RepeatOne { get; set; } = "<:repeatonce:682469899351621648>";
        public string RepeatOff { get; set; } = "<:repeatoff:682469899276517401>";
        public string Repeat { get; set; } = "<:repeat:682469899066409043>";
        public string Play { get; set; } = "<:play:682580118358458368>";
        public string Pause { get; set; } = "<:pause:682580118425960469>";
        public string Stop { get; set; } = "<:stop:682658172615524382>";
        public string Spotify { get; set; } = "<:spotify:764837934519156746>";
        public string RepeatBox {get;set;} = "<:repeatbox:854346340993597471>";
        public string RepeatOffBox {get;set;} = "<:repeatoffbox:854346381410172968>";
        public string RepeatOneBox {get;set;} = "<:repeatonebox:854346274416230421>";
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
    }
}