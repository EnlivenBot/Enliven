using System;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.MessageComponents;

public class EnlivenButtonBuilder : ButtonBuilder {
    public Guid Guid { get; } = Guid.NewGuid();

    /// <summary>
    /// Is <see cref="IsVisible"/> is <code>false</code> then this button will not get into MessageComponents when calling <see cref="EnlivenComponentBuilder.Build"/>
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Target row for current button
    /// </summary>
    public int TargetRow { get; set; } = 0;

    /// <summary>
    /// The higher the priority, the closer this button will be to the beginning of the row
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// This delegate would be called if user press this button
    /// </summary>
    public Action<SocketMessageComponent>? Callback { get; set; }

    public EnlivenButtonBuilder WithIsVisible(bool isVisible) {
        IsVisible = isVisible;
        return this;
    }

    public EnlivenButtonBuilder WithTargetRow(int targetRow) {
        TargetRow = targetRow;
        return this;
    }
    public EnlivenButtonBuilder WithPriority(int? priority) {
        Priority = priority;
        return this;
    }

    public EnlivenButtonBuilder WithCallback(Action<SocketMessageComponent>? callback) {
        Callback = callback;
        return this;
    }

    public EnlivenButtonBuilder Clone() {
        return new EnlivenButtonBuilder()
            .WithCallback(Callback)
            .WithPriority(Priority)
            .WithIsVisible(IsVisible)
            .WithTargetRow(TargetRow)
            .WithDisabled(IsDisabled)
            .WithCustomId(CustomId)
            .WithEmote(Emote)
            .WithLabel(Label)
            .WithStyle(Style)
            .WithUrl(Url);
    }

    /// <summary>
    ///     Sets the current buttons label to the specified text.
    /// </summary>
    /// <param name="label">The text for the label</param>
    /// <returns>The current builder.</returns>
    public new EnlivenButtonBuilder WithLabel(string label) {
        Label = label;
        return this;
    }

    /// <summary>Sets the current buttons style.</summary>
    /// <param name="style">The style for this builders button.</param>
    /// <returns>The current builder.</returns>
    public new EnlivenButtonBuilder WithStyle(ButtonStyle style) {
        Style = style;
        return this;
    }

    /// <summary>Sets the current buttons emote.</summary>
    /// <param name="emote">The emote to use for the current button.</param>
    /// <returns>The current builder.</returns>
    public new EnlivenButtonBuilder WithEmote(IEmote emote) {
        Emote = emote;
        return this;
    }

    /// <summary>Sets the current buttons url.</summary>
    /// <param name="url">The url to use for the current button.</param>
    /// <returns>The current builder.</returns>
    public new EnlivenButtonBuilder WithUrl(string url) {
        Url = url;
        return this;
    }

    /// <summary>Sets the custom id of the current button.</summary>
    /// <param name="id">The id to use for the current button.</param>
    /// <returns>The current builder.</returns>
    public new EnlivenButtonBuilder WithCustomId(string id) {
        CustomId = id;
        return this;
    }

    /// <summary>Sets whether the current button is disabled.</summary>
    /// <param name="disabled">Whether the current button is disabled or not.</param>
    /// <returns>The current builder.</returns>
    public new EnlivenButtonBuilder WithDisabled(bool disabled) {
        IsDisabled = disabled;
        return this;
    }
}