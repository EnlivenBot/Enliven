using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace Common.Config.Emoji;

public static class ProgressEmoji
{
    private static Logger _logger = LogManager.GetCurrentClassLogger();

    private static Lazy<ProgressEmojiList> _start = new(() =>
    {
        _logger.Info("Start loading ProgressStartEmoji");
        if (File.Exists(Path.Combine("Config", "ProgressStartEmoji.json")))
            return JsonConvert.DeserializeObject<ProgressEmojiList>(
                File.ReadAllText(Path.Combine("Config", "ProgressStartEmoji.json")));
        _logger.Info("ProgressStartEmoji.json not found, generating");
        var emoji = new ProgressEmojiList(
            "<:start0:667802061202522112>",
            "<:start2:667802134246457345>",
            "<:start4:667802171311390721>",
            "<:start6:667802208087179295>",
            "<:start8:667802227229982731>",
            "<:start10:667802240790167573>"
        );
        File.WriteAllText(Path.Combine("Config", "ProgressStartEmoji.json"),
            JsonConvert.SerializeObject(emoji, Formatting.Indented));
        return emoji;
    });

    private static Lazy<ProgressEmojiList> _intermediate = new(() =>
    {
        _logger.Info("Start loading ProgressIntermediateEmoji");
        if (File.Exists(Path.Combine("Config", "ProgressIntermediateEmoji.json")))
            return JsonConvert.DeserializeObject<ProgressEmojiList>(
                File.ReadAllText(Path.Combine("Config", "ProgressIntermediateEmoji.json")));
        _logger.Info("ProgressIntermediateEmoji.json not found, generating");
        var emoji = new ProgressEmojiList(
            "<:intermediate0:667802273987952663>",
            "<:intermediate2:667802286193377318>",
            "<:intermediate4:667802300714057747>",
            "<:intermediate6:667802315926929420>",
            "<:intermediate8:667802328782471175>",
            "<:intermediate10:667802348017418240>"
        );
        File.WriteAllText(Path.Combine("Config", "ProgressIntermediateEmoji.json"),
            JsonConvert.SerializeObject(emoji, Formatting.Indented));
        return emoji;
    });

    private static Lazy<ProgressEmojiList> _end = new(() =>
    {
        _logger.Info("Start loading ProgressEndEmoji");
        if (File.Exists(Path.Combine("Config", "ProgressEndEmoji.json")))
            return JsonConvert.DeserializeObject<ProgressEmojiList>(
                File.ReadAllText(Path.Combine("Config", "ProgressEndEmoji.json")));
        _logger.Info("ProgressEndEmoji.json not found, generating");
        var emoji = new ProgressEmojiList(
            "<:end0:667802364027338756>",
            "<:end2:667802384063266838>",
            "<:end4:667802394452557824>",
            "<:end6:667802408461533194>",
            "<:end8:667802418435588096>",
            "<:end10:667802433233354762>"
        );
        File.WriteAllText(Path.Combine("Config", "ProgressEndEmoji.json"),
            JsonConvert.SerializeObject(emoji, Formatting.Indented));
        return emoji;
    });

    private static Lazy<ProgressEmojiPack> _customEmojiPack = new(() =>
        new ProgressEmojiPack(_start.Value, _intermediate.Value, _end.Value));

    private static Lazy<ProgressEmojiPack> _textEmojiPack = new(() =>
        new ProgressEmojiPack(new ProgressEmojiList("<-", "<="), new ProgressEmojiList("―", "▬", "=="),
            new ProgressEmojiList("->", "=>")));

    public static ProgressEmojiPack CustomEmojiPack => _customEmojiPack.Value;

    public static ProgressEmojiPack TextEmojiPack => _textEmojiPack.Value;

    public static string GetEmoji(this ProgressEmojiList emojiList, int progress)
    {
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

    public class ProgressEmojiPack
    {
        public ProgressEmojiPack(ProgressEmojiList start, ProgressEmojiList intermediate, ProgressEmojiList end)
        {
            Start = start;
            Intermediate = intermediate;
            End = end;
        }

        private ProgressEmojiList Start { get; }
        private ProgressEmojiList Intermediate { get; }
        private ProgressEmojiList End { get; }

        public string GetProgress(int progress)
        {
            var builder = new StringBuilder();
            builder.Append(Start.GetEmoji(progress));
            progress -= 10;

            for (var i = 0; i < 8; i++)
            {
                builder.Append(Intermediate.GetEmoji(progress));
                progress -= 10;
            }

            builder.Append(End.GetEmoji(progress));
            return builder.ToString();
        }
    }
}