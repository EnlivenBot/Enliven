using System;
using Common.Config.Emoji;
using Common.Localization.Entries;
using Discord;

namespace Bot.DiscordRelated;

public class PaginatedAppearanceOptions {
    public static PaginatedAppearanceOptions Default = new();
    public IEmote Back = CommonEmoji.LegacyReverse;
    public bool DisplayInformationIcon;
    public IEmote First = CommonEmoji.LegacyTrackPrevious;


    public IEntry FooterFormat = new EntryString("{0}/{1}");
    public IEmote Info = CommonEmoji.Help;
    public string InformationText = "This is a paginator. React with the respective icons to change page.";
    public TimeSpan InfoTimeout = TimeSpan.FromSeconds(30);
    public IEmote Jump = CommonEmoji.InputNumbers;

    public JumpDisplayOptions JumpDisplayOptions = JumpDisplayOptions.WithManageMessages;
    public IEmote Last = CommonEmoji.LegacyTrackNext;
    public IEmote Next = CommonEmoji.LegacyPlay;
    public IEmote Stop = CommonEmoji.LegacyStop;

    public bool StopEnabled = true;

    public TimeSpan? Timeout = null;
    public PaginatedAppearanceOptions() { }

    public PaginatedAppearanceOptions(string informationText) {
        InformationText = informationText;
        DisplayInformationIcon = true;
    }
}