using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace Common.Config.Emoji;

public static class ProgressEmoji
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly Lazy<ProgressEmojiList> Start = new(() =>
    {
        Logger.Info("Start loading ProgressStartEmoji");
        var path = Path.Combine("Config", "ProgressStartEmoji.json");
        if (File.Exists(path))
            return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(path))!;
        Logger.Info("ProgressStartEmoji.json not found, generating");
        var emoji = new ProgressEmojiList(
            "<:start0:667802061202522112>",
            "<:start2:667802134246457345>",
            "<:start4:667802171311390721>",
            "<:start6:667802208087179295>",
            "<:start8:667802227229982731>",
            "<:start10:667802240790167573>"
        );
        File.WriteAllText(path, JsonConvert.SerializeObject(emoji, Formatting.Indented));
        return emoji;
    });

    private static readonly Lazy<ProgressEmojiList> Intermediate = new(() =>
    {
        Logger.Info("Start loading ProgressIntermediateEmoji");
        var path = Path.Combine("Config", "ProgressIntermediateEmoji.json");
        if (File.Exists(path))
            return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(path))!;
        Logger.Info("ProgressIntermediateEmoji.json not found, generating");
        var emoji = new ProgressEmojiList(
            "<:intermediate0:667802273987952663>",
            "<:intermediate2:667802286193377318>",
            "<:intermediate4:667802300714057747>",
            "<:intermediate6:667802315926929420>",
            "<:intermediate8:667802328782471175>",
            "<:intermediate10:667802348017418240>"
        );
        File.WriteAllText(path, JsonConvert.SerializeObject(emoji, Formatting.Indented));
        return emoji;
    });

    private static readonly Lazy<ProgressEmojiList> End = new(() =>
    {
        Logger.Info("Start loading ProgressEndEmoji");
        var path = Path.Combine("Config", "ProgressEndEmoji.json");
        if (File.Exists(path))
            return JsonConvert.DeserializeObject<ProgressEmojiList>(File.ReadAllText(path))!;
        Logger.Info("ProgressEndEmoji.json not found, generating");
        var emoji = new ProgressEmojiList(
            "<:end0:667802364027338756>",
            "<:end2:667802384063266838>",
            "<:end4:667802394452557824>",
            "<:end6:667802408461533194>",
            "<:end8:667802418435588096>",
            "<:end10:667802433233354762>"
        );
        File.WriteAllText(path, JsonConvert.SerializeObject(emoji, Formatting.Indented));
        return emoji;
    });

    private static readonly Lazy<ProgressEmojiPack> CustomEmojiPackLazy = new(() =>
        new ProgressEmojiPack(Start.Value, Intermediate.Value, End.Value));

    private static readonly Lazy<ProgressEmojiPack> TextEmojiPackLazy = new(() =>
        new ProgressEmojiPack(
            new ProgressEmojiList("<-", "<="), 
            new ProgressEmojiList("―", "▬", "=="),
            new ProgressEmojiList("->", "=>")));

    public static ProgressEmojiPack CustomEmojiPack => CustomEmojiPackLazy.Value;

    public static ProgressEmojiPack TextEmojiPack => TextEmojiPackLazy.Value;

    public static string GetEmoji(this ProgressEmojiList emojiList, int progress)
    {
        return progress switch
        {
            <= 0 => emojiList.Part0,
            <= 2 => emojiList.Part2,
            <= 4 => emojiList.Part4,
            <= 6 => emojiList.Part6,
            <= 8 => emojiList.Part8,
            _ => emojiList.Part10
        };
    }

    public class ProgressEmojiPack(ProgressEmojiList start, ProgressEmojiList intermediate, ProgressEmojiList end)
    {
        private ProgressEmojiList Start { get; } = start;
        private ProgressEmojiList Intermediate { get; } = intermediate;
        private ProgressEmojiList End { get; } = end;

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