using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Bot.Utilities.Emoji {
    public class ProgressEmojiList {
        public ProgressEmojiList() { }

        public ProgressEmojiList(string part0, string part2, string part4, string part6, string part8, string part10) {
            Part0 = part0;
            Part2 = part2;
            Part4 = part4;
            Part6 = part6;
            Part8 = part8;
            Part10 = part10;
        }

        public ProgressEmojiList(string part) : this(part, part, part, part, part, part) { }
        public ProgressEmojiList(string empty, string full) : this(empty, empty, empty, full, full, full) { }
        public ProgressEmojiList(string empty, string half, string full) : this(empty, empty, half, half, full, full) { }
        public string Part0 { get; set; }
        public string Part2 { get; set; }
        public string Part4 { get; set; }
        public string Part6 { get; set; }
        public string Part8 { get; set; }
        public string Part10 { get; set; }
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

        private static Lazy<ProgressEmojiPack> _customEmojiPack = new Lazy<ProgressEmojiPack>(() => new ProgressEmojiPack {
            Start = _start.Value,
            End = _end.Value,
            Intermediate = _intermediate.Value
        });

        public static ProgressEmojiPack CustomEmojiPack => _customEmojiPack.Value;

        private static Lazy<ProgressEmojiPack> _textEmojiPack = new Lazy<ProgressEmojiPack>(() => new ProgressEmojiPack {
            Start = new ProgressEmojiList("<-", "<="),
            End = new ProgressEmojiList("->", "=>"),
            Intermediate = new ProgressEmojiList("―", "▬", "==")
        });

        public static ProgressEmojiPack TextEmojiPack => _textEmojiPack.Value;

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

        public class ProgressEmojiPack {
            public ProgressEmojiList Start { get; set; }
            public ProgressEmojiList Intermediate { get; set; }
            public ProgressEmojiList End { get; set; }

            public string GetProgress(int progress) {
                var builder = new StringBuilder();
                builder.Append(Start.GetEmoji(progress));
                progress -= 10;

                for (var i = 0; i < 8; i++) {
                    builder.Append(Intermediate.GetEmoji(progress));
                    progress -= 10;
                }

                builder.Append(End.GetEmoji(progress));
                return builder.ToString();
            }
        }
    }
}