using System;

namespace Common {
    public static class Constants {
        public static TimeSpan LongTimeSpan { get; set; } = TimeSpan.FromMinutes(5);
        public static TimeSpan StandardTimeSpan { get; set; } = TimeSpan.FromMinutes(2);
        public static TimeSpan ShortTimeSpan { get; set; } = TimeSpan.FromMinutes(1);
        public static TimeSpan VeryShortTimeSpan { get; set; } = TimeSpan.FromSeconds(30);

        public static int MaxQueueHistoryChars { get; set; } = 512;
        public static int MaxTracksCount { get; set; } = 2000;

        public static int MaxEmbedAuthorLength { get; set; } = 256;
        public static int MaxFieldLength { get; set; } = 2048;

        public static TimeSpan PlayerEmbedUpdateDelay { get; set; } = TimeSpan.FromSeconds(5);

        public const string BotLifetimeScopeTag = "BotInstance";
    }
}