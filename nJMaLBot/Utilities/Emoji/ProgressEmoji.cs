using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NLog;

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
        public string Part0 { get; set; } = null!;
        public string Part2 { get; set; } = null!;
        public string Part4 { get; set; } = null!;
        public string Part6 { get; set; } = null!;
        public string Part8 { get; set; } = null!;
        public string Part10 { get; set; } = null!;
    }

    public static class ProgressEmoji {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static Lazy<ProgressEmojiList> _start = new Lazy<ProgressEmojiList>(() => {
            logger.Info("Start loading ProgressStartEmoji");
            if (File.Exists(Path.Combine("Config", "ProgressStartEmoji.json")))
                return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(Path.Combine("Config", "ProgressStartEmoji.json")));
            logger.Info("ProgressStartEmoji.json not found, generating");
            var emoji = new ProgressEmojiList(
                "<:start0:667802061202522112>",
                "<:start2:667802134246457345>",
                "<:start4:667802171311390721>",
                "<:start6:667802208087179295>",
                "<:start8:667802227229982731>",
                "<:start10:667802240790167573>"
            );
            File.WriteAllText(Path.Combine("Config", "ProgressStartEmoji.json"), JsonConvert.SerializeObject(emoji, Formatting.Indented));
            return emoji;
        });

        private static Lazy<ProgressEmojiList> _intermediate = new Lazy<ProgressEmojiList>(() => {
            logger.Info("Start loading ProgressIntermediateEmoji");
            if (File.Exists(Path.Combine("Config", "ProgressIntermediateEmoji.json")))
                return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(Path.Combine("Config", "ProgressIntermediateEmoji.json")));
            logger.Info("ProgressIntermediateEmoji.json not found, generating");
            var emoji = new ProgressEmojiList(
                "<:intermediate0:667802273987952663>",
                "<:intermediate2:667802286193377318>",
                "<:intermediate4:667802300714057747>",
                "<:intermediate6:667802315926929420>",
                "<:intermediate8:667802328782471175>",
                "<:intermediate10:667802348017418240>"
            );
            File.WriteAllText(Path.Combine("Config", "ProgressIntermediateEmoji.json"), JsonConvert.SerializeObject(emoji, Formatting.Indented));
            return emoji;
        });

        private static Lazy<ProgressEmojiList> _end = new Lazy<ProgressEmojiList>(() => {
            logger.Info("Start loading ProgressEndEmoji");
            if (File.Exists(Path.Combine("Config", "ProgressEndEmoji.json")))
                return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(Path.Combine("Config", "ProgressEndEmoji.json")));
            logger.Info("ProgressEndEmoji.json not found, generating");
            var emoji = new ProgressEmojiList(
                "<:end0:667802364027338756>",
                "<:end2:667802384063266838>",
                "<:end4:667802394452557824>",
                "<:end6:667802408461533194>",
                "<:end8:667802418435588096>",
                "<:end10:667802433233354762>"
            );
            File.WriteAllText(Path.Combine("Config", "ProgressEndEmoji.json"), JsonConvert.SerializeObject(emoji, Formatting.Indented));
            return emoji;
        });

        private static Lazy<ProgressEmojiPack> _customEmojiPack = new Lazy<ProgressEmojiPack>(() =>
            new ProgressEmojiPack(_start.Value, _intermediate.Value, _end.Value));

        public static ProgressEmojiPack CustomEmojiPack => _customEmojiPack.Value;

        private static Lazy<ProgressEmojiPack> _textEmojiPack = new Lazy<ProgressEmojiPack>(() =>
            new ProgressEmojiPack(new ProgressEmojiList("<-", "<="), new ProgressEmojiList("―", "▬", "=="), new ProgressEmojiList("->", "=>")));

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
            public ProgressEmojiPack(ProgressEmojiList start, ProgressEmojiList intermediate, ProgressEmojiList end) {
                Start = start;
                Intermediate = intermediate;
                End = end;
            }

            private ProgressEmojiList Start { get; }
            private ProgressEmojiList Intermediate { get; }
            private ProgressEmojiList End { get; }

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