using System;
using System.IO;
using Newtonsoft.Json;

namespace Bot.Utilities.Emoji {
    public class ProgressEmojiList {
        public string Part0 { get; set; } = "<:start0:667802061202522112>";
        public string Part2 { get; set; } = "<:start2:667802134246457345>";
        public string Part4 { get; set; } = "<:start4:667802171311390721>";
        public string Part6 { get; set; } = "<:start6:667802208087179295>";
        public string Part8 { get; set; } = "<:start8:667802227229982731>";
        public string Part10 { get; set; } = "<:start10:667802240790167573>";
    }

    public static class ProgressEmoji {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static Lazy<ProgressEmojiList> _start = new Lazy<ProgressEmojiList>(() => {
            logger.Info("Start loading ProgressStartEmoji");
            if (File.Exists(Path.Combine("Config", "ProgressStartEmoji.json")))
                return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(Path.Combine("Config", "ProgressStartEmoji.json")));
            logger.Info("ProgressStartEmoji.json not found, generating");
            var emoji = new ProgressEmojiList {
                Part0 = "<:start0:667802061202522112>",
                Part2 = "<:start2:667802134246457345>",
                Part4 = "<:start4:667802171311390721>",
                Part6 = "<:start6:667802208087179295>",
                Part8 = "<:start8:667802227229982731>",
                Part10 = "<:start10:667802240790167573>"
            };
            File.WriteAllText(Path.Combine("Config", "ProgressStartEmoji.json"), JsonConvert.SerializeObject(emoji, Formatting.Indented));
            return emoji;
        });

        private static Lazy<ProgressEmojiList> _intermediate = new Lazy<ProgressEmojiList>(() => {
            logger.Info("Start loading ProgressIntermediateEmoji");
            if (File.Exists(Path.Combine("Config", "ProgressIntermediateEmoji.json")))
                return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(Path.Combine("Config", "ProgressIntermediateEmoji.json")));
            logger.Info("ProgressIntermediateEmoji.json not found, generating");
            var emoji = new ProgressEmojiList {
                Part0 = "<:intermediate0:667802273987952663>",
                Part2 = "<:intermediate2:667802286193377318>",
                Part4 = "<:intermediate4:667802300714057747>",
                Part6 = "<:intermediate6:667802315926929420>",
                Part8 = "<:intermediate8:667802328782471175>",
                Part10 = "<:intermediate10:667802348017418240>"
            };
            File.WriteAllText(Path.Combine("Config", "ProgressIntermediateEmoji.json"), JsonConvert.SerializeObject(emoji, Formatting.Indented));
            return emoji;
        });

        private static Lazy<ProgressEmojiList> _end = new Lazy<ProgressEmojiList>(() => {
            logger.Info("Start loading ProgressEndEmoji");
            if (File.Exists(Path.Combine("Config", "ProgressEndEmoji.json")))
                return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(Path.Combine("Config", "ProgressEndEmoji.json")));
            logger.Info("ProgressEndEmoji.json not found, generating");
            var emoji = new ProgressEmojiList {
                Part0 = "<:end0:667802364027338756>",
                Part2 = "<:end2:667802384063266838>",
                Part4 = "<:end4:667802394452557824>",
                Part6 = "<:end6:667802408461533194>",
                Part8 = "<:end8:667802418435588096>",
                Part10 = "<:end10:667802433233354762>"
            };
            File.WriteAllText(Path.Combine("Config", "ProgressEndEmoji.json"), JsonConvert.SerializeObject(emoji, Formatting.Indented));
            return emoji;
        });

        public static ProgressEmojiList Start => _start.Value;
        public static ProgressEmojiList Intermediate => _intermediate.Value;
        public static ProgressEmojiList End => _end.Value;

        public static string GetEmoji(this ProgressEmojiList emojiList, int progress) {
            if (progress <= 0)
                return emojiList.Part0;

            if (progress <= 2)
                return emojiList.Part2;

            if (progress <= 4)
                return emojiList.Part4;

            if (progress <= 6)
                return emojiList.Part6;

            if (progress <= 8)
                return emojiList.Part8;

            return emojiList.Part10;
        }
    }
}