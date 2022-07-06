using Discord;

namespace Common.Config.Emoji {
    public static class CommonEmoji {
        public static Emote RepeatOne { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOne);
        public static Emote RepeatOff { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOff);
        public static Emote Repeat { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Repeat);
        public static Emote Play { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Play);
        public static Emote Pause { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Pause);
        public static Emote Stop { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Stop);
        public static Emote Spotify { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Spotify);
        public static Emote RepeatBox { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatBox);
        public static Emote RepeatOneBox { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOneBox);
        public static Emote RepeatOffBox { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOffBox);
        public static global::Discord.Emoji LegacyTrackNext { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyTrackNext);
        public static global::Discord.Emoji LegacyTrackPrevious { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyTrackPrevious);
        public static global::Discord.Emoji LegacyPause { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyPause);
        public static global::Discord.Emoji LegacyPlay { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyPlay);
        public static global::Discord.Emoji LegacyStop { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyStop);
        public static global::Discord.Emoji LegacySound { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacySound);
        public static global::Discord.Emoji LegacyLoudSound { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyLoudSound);
        public static global::Discord.Emoji LegacyRepeat { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyRepeat);
        public static global::Discord.Emoji LegacyShuffle { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyShuffle);
        public static global::Discord.Emoji LegacyBook { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyBook);
        public static global::Discord.Emoji LegacyPlayPause { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyPlayPause);
        public static global::Discord.Emoji LegacyArrowDown { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyArrowDown);
        public static global::Discord.Emoji LegacyFileBox { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyFileBox);
        public static global::Discord.Emoji LegacyReverse { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyReverse);
        public static global::Discord.Emoji LegacyFastForward { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyFastForward);
        public static global::Discord.Emoji LegacyFastReverse { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LegacyFastReverse);
        public static global::Discord.Emoji Help { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.Help);
        public static global::Discord.Emoji Memo { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.Memo);
        public static global::Discord.Emoji Robot { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.Robot);
        public static global::Discord.Emoji ExclamationPoint { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.ExclamationPoint);
        public static global::Discord.Emoji Printer { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.Printer);
        public static global::Discord.Emoji InputNumbers { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.InputNumbers);
        public static global::Discord.Emoji ThumbsUp { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.ThumbsUp);
        public static global::Discord.Emoji ThumbsDown { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.ThumbsDown);
        public static global::Discord.Emoji Warning { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.Warning);
        public static global::Discord.Emoji BookmarkTabs { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.BookmarkTabs);
        public static global::Discord.Emoji NoEntry { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.NoEntry);
        public static global::Discord.Emoji Level { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.Level);
        public static global::Discord.Emoji E { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.E);
        
        // Animated
        public static global::Discord.Emoji LoadingAnimated { get; set; } = new global::Discord.Emoji(CommonEmojiStrings.Instance.LoadingAnimated);
    }
}