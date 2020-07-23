using System;

namespace Bot.Utilities {
    public static class Constants {
        public static TimeSpan LongTimeSpan = TimeSpan.FromMinutes(10);
        public static TimeSpan StandardTimeSpan = TimeSpan.FromMinutes(5);
        public static TimeSpan ShortTimeSpan = TimeSpan.FromMinutes(3);
        public static TimeSpan VeryShortTimeSpan = TimeSpan.FromMinutes(2);

        public static int MaxQueueHistoryChars = 512;
        public static int MaxTracksCount = 2000;

        public static int MaxEmbedAuthorLength = 256;
        public static int MaxFieldLength = 2048;
        
        public static TimeSpan PlayerEmbedUpdateDelay = TimeSpan.FromSeconds(4);
    }
}