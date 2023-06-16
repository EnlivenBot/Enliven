using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Criteria;
using Bot.DiscordRelated.Music;
using Bot.Utilities;
using Common;
using Common.Criteria;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Utils;
using Discord;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated {
    public class NonSpamMessageController {
        private static readonly Regex UserMentionRegex = new(@"(?<!<@)!?(\d+)(?=>)");
        private readonly ILocalizationProvider _loc;

        private readonly SingleTask<IUserMessage?> _resendTask;
        private readonly SingleTask _updateTask;

        private HandyLongestTimer _clearTimer = new();

        private TimeChecker _lastSendTimeChecker = new(TimeSpan.FromSeconds(20));
        private SendControlMessageOverride? _sendControlMessageOverride;

        public NonSpamMessageController(ILocalizationProvider loc, IMessageChannel channel, string embedTitle, Color embedColor = default) {
            _loc = loc;
            TargetChannel = channel;
            EmbedTitle = embedTitle;
            EmbedColor = embedColor;
            _updateTask = new SingleTask(InternalUpdateAsync) { CanBeDirty = true, BetweenExecutionsDelay = TimeSpan.FromSeconds(1) };
            _resendTask = new SingleTask<IUserMessage?>(InternalResendAsync);
        }
        public string EmbedTitle { get; set; }
        public Color EmbedColor { get; set; }

        private IUserMessage? Message { get; set; }
        private List<MessageControllerEntryData> Entries { get; } = new();
        private IMessageChannel TargetChannel { get; }

        [Obsolete("Use overload with IEntry instead")]
        public NonSpamMessageController AddEntry(string entry, TimeSpan? timeout = null) {
            AddEntryInternal(new EntryString(entry), timeout);
            return this;
        }

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
            Entries.Add(data);
            _clearTimer.SetDelay(data.Timeout);
            return data;
        }

        public NonSpamMessageController RemoveEntry(IEntry entry) {
            var data = Entries
                .Where(data => data.Entry == entry)
                .MinBy(data => data.AddDate);
            if (data != null) {
                Entries.Remove(data);
            }
            return this;
        }

        public Embed? GetEmbed() {
            Entries.RemoveAll(data => data.AddDate + data.Timeout < DateTime.Now);
            if (Entries.Count == 0) return null;

            var description = Entries
                .GroupBy(data => data.Entry.Get(_loc))
                .Select(grouping => grouping.Key + GetEntryPostfix(grouping.Count(), grouping.OrderBy(data => data.AddDate).Last().AddDate))
                .JoinToString("\n");
            return new EmbedBuilder()
                .WithTitle(EmbedTitle)
                .WithDescription(description)
                .Build();

            string GetEntryPostfix(int count, DateTime last) {
                var lastOffset = new DateTimeOffset(last);
                return count == 1 ? $" (<t:{lastOffset.ToUnixTimeSeconds()}:R>)" : $" (**{count}x**, last <t:{lastOffset.ToUnixTimeSeconds()}:R>)";
            }
        }

        public Task Update() {
            return _updateTask.Execute();
        }

        public Task<IUserMessage?> Resend() {
            return _resendTask.Execute();
        }

        private async Task<IUserMessage?> InternalResendAsync(SingleTaskExecutionData executionData) {
            Message?.SafeDelete();
            Message = null;

            var embed = GetEmbed();
            if (embed == null) {
                executionData.OverrideDelay = TimeSpan.Zero;
                return null;
            }


            try {
                _lastSendTimeChecker.Update();
                Message = await _sendControlMessageOverride.ExecuteAndFallbackWith(embed, null, TargetChannel);
                _sendControlMessageOverride = null;
                return Message;
            }
            catch {
                // ignored
            }

            return null;
        }

        private async Task InternalUpdateAsync(SingleTaskExecutionData executionData) {
            var embed = GetEmbed();
            if (embed == null) {
                Message.SafeDelete();
                Message = null;
                executionData.OverrideDelay = TimeSpan.Zero;
                return;
            }

            try {
                if (Message == null || await _lastSendTimeChecker.ToCriteria().AddCriterion(new EnsureMessage(TargetChannel, Message).Invert()).JudgeAsync())
                    await Resend();
                else {
                    await Message!.ModifyAsync(properties => {
                        properties.Embed = embed;
                        var matchCollection = UserMentionRegex.Matches(properties.Embed.Value.Description);
                        properties.Content = matchCollection.Select(match => $"<@{match.Value}>").JoinToString(", ");
                    });
                }
            }
            catch {
                // ignored
            }
        }

        public async Task ResendWithOverride(SendControlMessageOverride sendControlMessageOverride, bool executeResend = true) {
            _sendControlMessageOverride = (embed, component) => {
                _sendControlMessageOverride = null;
                return sendControlMessageOverride(embed, component);
            };
            if (!executeResend) return;

            await _resendTask.Execute(false, TimeSpan.Zero);
        }

        private record MessageControllerEntryData(IEntry Entry, TimeSpan Timeout) {
            public IEntry Entry { get; set; } = Entry;
            public TimeSpan Timeout { get; } = Timeout;
            public DateTime AddDate { get; } = DateTime.Now;
        }

        private class NonSpamMessageControllerEntry : IRepliedEntry {
            private readonly NonSpamMessageController _controller;
            private bool _isDeleted;
            public NonSpamMessageControllerEntry(NonSpamMessageController controller, MessageControllerEntryData data) {
                _controller = controller;
                Data = data;
            }
            public MessageControllerEntryData Data { get; private set; }
            public Task ChangeEntryAsync(IEntry entry) {
                if (_isDeleted) throw new InvalidOperationException("This IEntry already deleted");
                Data.Entry = entry;
                return _controller.Update();
            }
            public Task DeleteAsync() {
                if (_isDeleted) return Task.CompletedTask;
                _isDeleted = true;
                _controller.Entries.Remove(Data);
                return _controller.Update();
            }
        }
    }
}