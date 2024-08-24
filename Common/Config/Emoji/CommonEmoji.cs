using Discord;

namespace Common.Config.Emoji;

public static class CommonEmoji
{
    public static Emote RepeatOne { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOne);
    public static Emote RepeatOff { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOff);
    public static Emote Repeat { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Repeat);
    public static Emote Play { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Play);
    public static Emote Pause { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Pause);
    public static Emote Stop { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Stop);
    public static Emote Spotify { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.Spotify);
    public static Emote YandexMusic { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.YandexMusic);
    public static Emote VkMusic { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.VkMusic);
    public static Emote RepeatBox { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatBox);
    public static Emote RepeatOneBox { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOneBox);
    public static Emote RepeatOffBox { get; set; } = Emote.Parse(CommonEmojiStrings.Instance.RepeatOffBox);
    public static Discord.Emoji LegacyTrackNext { get; set; } = new(CommonEmojiStrings.Instance.LegacyTrackNext);

    public static Discord.Emoji LegacyTrackPrevious { get; set; } =
        new(CommonEmojiStrings.Instance.LegacyTrackPrevious);

    public static Discord.Emoji LegacyPause { get; set; } = new(CommonEmojiStrings.Instance.LegacyPause);
    public static Discord.Emoji LegacyPlay { get; set; } = new(CommonEmojiStrings.Instance.LegacyPlay);
    public static Discord.Emoji LegacyStop { get; set; } = new(CommonEmojiStrings.Instance.LegacyStop);
    public static Discord.Emoji LegacySound { get; set; } = new(CommonEmojiStrings.Instance.LegacySound);
    public static Discord.Emoji LegacyLoudSound { get; set; } = new(CommonEmojiStrings.Instance.LegacyLoudSound);
    public static Discord.Emoji LegacyRepeat { get; set; } = new(CommonEmojiStrings.Instance.LegacyRepeat);
    public static Discord.Emoji LegacyShuffle { get; set; } = new(CommonEmojiStrings.Instance.LegacyShuffle);
    public static Discord.Emoji LegacyBook { get; set; } = new(CommonEmojiStrings.Instance.LegacyBook);
    public static Discord.Emoji LegacyPlayPause { get; set; } = new(CommonEmojiStrings.Instance.LegacyPlayPause);
    public static Discord.Emoji LegacyArrowDown { get; set; } = new(CommonEmojiStrings.Instance.LegacyArrowDown);
    public static Discord.Emoji LegacyFileBox { get; set; } = new(CommonEmojiStrings.Instance.LegacyFileBox);
    public static Discord.Emoji LegacyReverse { get; set; } = new(CommonEmojiStrings.Instance.LegacyReverse);
    public static Discord.Emoji LegacyFastForward { get; set; } = new(CommonEmojiStrings.Instance.LegacyFastForward);
    public static Discord.Emoji LegacyFastReverse { get; set; } = new(CommonEmojiStrings.Instance.LegacyFastReverse);
    public static Discord.Emoji Help { get; set; } = new(CommonEmojiStrings.Instance.Help);
    public static Discord.Emoji Memo { get; set; } = new(CommonEmojiStrings.Instance.Memo);
    public static Discord.Emoji Robot { get; set; } = new(CommonEmojiStrings.Instance.Robot);
    public static Discord.Emoji ExclamationPoint { get; set; } = new(CommonEmojiStrings.Instance.ExclamationPoint);
    public static Discord.Emoji Printer { get; set; } = new(CommonEmojiStrings.Instance.Printer);
    public static Discord.Emoji InputNumbers { get; set; } = new(CommonEmojiStrings.Instance.InputNumbers);
    public static Discord.Emoji ThumbsUp { get; set; } = new(CommonEmojiStrings.Instance.ThumbsUp);
    public static Discord.Emoji ThumbsDown { get; set; } = new(CommonEmojiStrings.Instance.ThumbsDown);
    public static Discord.Emoji Warning { get; set; } = new(CommonEmojiStrings.Instance.Warning);
    public static Discord.Emoji BookmarkTabs { get; set; } = new(CommonEmojiStrings.Instance.BookmarkTabs);
    public static Discord.Emoji NoEntry { get; set; } = new(CommonEmojiStrings.Instance.NoEntry);
    public static Discord.Emoji Level { get; set; } = new(CommonEmojiStrings.Instance.Level);
    public static Discord.Emoji E { get; set; } = new(CommonEmojiStrings.Instance.E);

    // Animated
    public static Discord.Emoji LoadingAnimated { get; set; } = new(CommonEmojiStrings.Instance.LoadingAnimated);
}