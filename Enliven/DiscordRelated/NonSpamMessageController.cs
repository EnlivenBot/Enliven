using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions.Wrappers;
using Bot.DiscordRelated.UpdatableMessage;
using Common;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Utils;
using Discord;
using Discord.WebSocket;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated;

public partial class NonSpamMessageController : DisposableBase {
    [GeneratedRegex(@"(?<=<@)(\d+)(?=>)")] private static partial Regex UserMentionRegex { get; }

    private readonly ILocalizationProvider _loc;
    private readonly HandyTimer _clearTimer = new();
    private readonly UpdatableMessageDisplay _updatableMessageDisplay;
    private readonly List<MessageControllerEntryData> _entries = [];

    public NonSpamMessageController(ILocalizationProvider loc, BaseSocketClient enlivenShardedClient,
        IMessageChannel channel, string embedTitle, Color embedColor = default) {
        _loc = loc;
        EmbedTitle = embedTitle;
        EmbedColor = embedColor;
        _updatableMessageDisplay = new UpdatableMessageDisplay(channel, MessagePropertiesUpdateCallback, null);
        // @formatter:off
        _updatableMessageDisplay.AttachBehavior(new ResendAfterTimeUpdatableMessageDisplayBehavior(TimeSpan.FromSeconds(20)));
        _updatableMessageDisplay.AttachBehavior(new StayInTheViewUpdatableMessageDisplayBehavior(enlivenShardedClient, 1));
        // @formatter:on

        _clearTimer.OnTimerElapsed
            .Delay(TimeSpan.FromSeconds(5))
            .Subscribe(OnClearingRequested);
    }

    public string EmbedTitle { get; private set; }
    public Color EmbedColor { get; private set; }

    public NonSpamMessageController AddEntry(IEntry entry, TimeSpan? timeout = null) {
        AddEntryInternal(entry, timeout);
        return this;
    }

    public IRepliedEntry AddRepliedEntry(IEntry entry, TimeSpan? timeout = null) {
        var data = AddEntryInternal(entry, timeout);
        return new NonSpamMessageControllerEntry(this, data);
    }

    private MessageControllerEntryData AddEntryInternal(IEntry entry, TimeSpan? timeout = null) {
        var data = new MessageControllerEntryData(entry, timeout.GetValueOrDefault(Constants.ShortTimeSpan));
        _entries.Add(data);
        _clearTimer.SetDelay(data.Timeout);
        OnEntriesChanged();
        return data;
    }

    public NonSpamMessageController RemoveEntry(IEntry entry) {
        var data = _entries
            .Where(data => data.Entry == entry)
            .MinBy(data => data.AddDate);
        if (data != null) _entries.Remove(data);
        OnEntriesChanged();
        return this;
    }

    public Task Update() {
        return _updatableMessageDisplay.Update(false);
    }

    public Task Update(IEnlivenInteraction interaction) {
        return _updatableMessageDisplay.HandleInteraction(interaction);
    }

    public bool IsEmpty {
        get {
            _entries.RemoveAll(data => data.AddDate + data.Timeout < DateTime.Now);
            return _entries.Count == 0;
        }
    }

    private Embed? GetEmbed() {
        _entries.RemoveAll(data => data.AddDate + data.Timeout < DateTime.Now);
        if (_entries.Count == 0) return null;

        var description = _entries
            .GroupBy(data => data.Entry.Get(_loc))
            .Select(grouping => grouping.Key + GetEntryPostfix(grouping.Count(), grouping.Max(data => data.AddDate)))
            .JoinToString("\n");

        return new EmbedBuilder()
            .WithTitle(EmbedTitle)
            .WithDescription(description)
            .WithColor(EmbedColor)
            .Build();

        string GetEntryPostfix(int count, DateTime last) {
            var lastOffset = new DateTimeOffset(last);
            return count == 1
                ? $" (<t:{lastOffset.ToUnixTimeSeconds()}:R>)"
                : $" (**{count}x**, last <t:{lastOffset.ToUnixTimeSeconds()}:R>)";
        }
    }

    private void MessagePropertiesUpdateCallback(MessageProperties properties) {
        properties.Embed = GetEmbed();
        var matchCollection = UserMentionRegex.Matches(properties.Embed.Value.Description);
        properties.Content = matchCollection.Count > 0
            ? matchCollection.Select(match => $"<@{match.Value}>").JoinToString(", ")
            : Optional<string>.Unspecified;
    }

    private void OnEntriesChanged() {
        _clearTimer.SetDelay(_entries.Count > 0 ? _entries.Min(data => data.Timeout) : TimeSpan.Zero);
    }

    private void OnClearingRequested(Unit __) {
        if (IsDisposed || !IsEmpty) return;
        _ = Task.Run(async () => {
            var message = await DisposeAsync();
            _ = message?.DeleteAsync().ObserveException();
        });
    }

    public async ValueTask<InteractionMessageHolder?> DisposeAsync() {
        var holder = Dispose();
        await _updatableMessageDisplay.DisposeAsync();
        return holder;
    }

    public new InteractionMessageHolder? Dispose() {
        return _updatableMessageDisplay.Dispose();
    }

    protected override void DisposeInternal() {
        _clearTimer.Dispose();
    }

    private record MessageControllerEntryData(IEntry Entry, TimeSpan Timeout) {
        public IEntry Entry { get; set; } = Entry;
        public TimeSpan Timeout { get; } = Timeout;
        public DateTime AddDate { get; } = DateTime.Now;
    }

    private record NonSpamMessageControllerEntry(NonSpamMessageController Controller, MessageControllerEntryData Data)
        : IRepliedEntry {
        private bool _isDeleted;

        public void Delete() {
            if (_isDeleted) return;
            _isDeleted = true;
            Controller._entries.Remove(Data);
            Controller.OnEntriesChanged();
        }
    }
}