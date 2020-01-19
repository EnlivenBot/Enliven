namespace Bot.Music {
    public interface IProgressEmoji {
        string Part0 { get; }
        string Part2 { get; }
        string Part4 { get; }
        string Part6 { get; }
        string Part8 { get; }
        string Part10 { get; }
    }

    public static class ProgressEmoji {
        public static ProgressStartEmoji Start = new ProgressStartEmoji();
        public static ProgressIntermediateEmoji Intermediate = new ProgressIntermediateEmoji();
        public static ProgressEndEmoji End = new ProgressEndEmoji();

        public static string GetEmoji(this IProgressEmoji emojiList, int progress) {
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

    public class ProgressStartEmoji : IProgressEmoji {
        public string Part0 { get; } = "<:start0:667802061202522112>";
        public string Part2 { get; } = "<:start2:667802134246457345>";
        public string Part4 { get; } = "<:start4:667802171311390721>";
        public string Part6 { get; } = "<:start6:667802208087179295>";
        public string Part8 { get; } = "<:start8:667802227229982731>";
        public string Part10 { get; } = "<:start10:667802240790167573>";
    }

    public class ProgressIntermediateEmoji : IProgressEmoji {
        public string Part0 { get; } = "<:intermediate0:667802273987952663>";
        public string Part2 { get; } = "<:intermediate2:667802286193377318>";
        public string Part4 { get; } = "<:intermediate4:667802300714057747>";
        public string Part6 { get; } = "<:intermediate6:667802315926929420>";
        public string Part8 { get; } = "<:intermediate8:667802328782471175>";
        public string Part10 { get; } = "<:intermediate10:667802348017418240>";
    }

    public class ProgressEndEmoji : IProgressEmoji {
        public string Part0 { get; } = "<:end0:667802364027338756>";
        public string Part2 { get; } = "<:end2:667802384063266838>";
        public string Part4 { get; } = "<:end4:667802394452557824>";
        public string Part6 { get; } = "<:end6:667802408461533194>";
        public string Part8 { get; } = "<:end8:667802418435588096>";
        public string Part10 { get; } = "<:end10:667802433233354762>";
    }
}