using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Common.Localization.Providers;
using DiffMatchPatch;

namespace Common.Entities {
    public class MessageSnapshot {
        public MessageSnapshot(MessageHistory messageHistory, DateTimeOffset editTimestamp, int changeIndex, string currentContent, string? previousMessageContent) :
            this(messageHistory, editTimestamp, changeIndex, currentContent, previousMessageContent, false) { }

        private MessageSnapshot(MessageHistory messageHistory, DateTimeOffset editTimestamp, int changeIndex, string currentContent, string? previousMessageContent, bool isAboutHistoryUnavailability) {
            MessageHistory = messageHistory;
            EditTimestamp = editTimestamp;
            PreviousMessageContent = previousMessageContent;
            IsAboutHistoryUnavailability = isAboutHistoryUnavailability;
            CurrentContent = currentContent;
            ChangeIndex = changeIndex;
        }

        public static MessageSnapshot WithMessageUnavailable(MessageHistory messageHistory, ILocalizationProvider loc) {
            return new MessageSnapshot(messageHistory, default, -1, loc.Get("MessageHistory.PreviousUnavailable"), null, true);
        }

        public IImmutableList<Diff>? GetEdits() {
            return ChangeIndex switch {
                < 0 => null,
                0   => new List<Diff> {new(Operation.Equal, CurrentContent)}.ToImmutableList(),
                _   => Diff.Compute(PreviousMessageContent ?? "", CurrentContent, true, true, CancellationToken.None).MakeHumanReadable()
            };
        }

        public MessageHistory MessageHistory { get; }
        public DateTimeOffset EditTimestamp { get; }
        public string? PreviousMessageContent { get; }
        public string CurrentContent { get; }
        public int ChangeIndex { get; }
        public bool IsAboutHistoryUnavailability { get; }
    }
}