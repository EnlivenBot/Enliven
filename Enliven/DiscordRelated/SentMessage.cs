using System;
using System.Threading.Tasks;
using Common;
using Discord;

namespace Bot.DiscordRelated {
    public sealed class SentMessage {
        private readonly Task<IUserMessage>? _cachedMessage;
        private Task<IUserMessage>? _resolveMessageTask;
        private readonly Func<Task<IUserMessage>>? _resolveMessage;
        private readonly bool? _isEphemeral;

        public SentMessage(IUserMessage cachedMessage, bool? isEphemeral) {
            _isEphemeral = isEphemeral;
            _cachedMessage = Task.FromResult(cachedMessage);
        }

        public SentMessage(Func<Task<IUserMessage>> resolveMessage, bool? isEphemeral) {
            _resolveMessage = resolveMessage;
            _isEphemeral = isEphemeral;
        }

        public SentMessage(Task<IUserMessage> messageTask, bool? isEphemeral) {
            _cachedMessage = messageTask;
            _isEphemeral = isEphemeral;
        }

        public Task<IUserMessage> GetMessageAsync() {
            if (_cachedMessage != null)
                return _cachedMessage;
            return _resolveMessageTask ??= _resolveMessage!();
        }

        public void CleanupAfter(TimeSpan delay) {
            if (_isEphemeral == true)
                return;
            _ = GetMessageAsync().DelayedDelete(delay);
        }
        
        /// <returns>
        /// Task, which completed when target message was deleted (or <c>Task.CompletedTask</c> when nothing to clean)
        /// </returns>
        public Task CleanupAfterAsync(TimeSpan delay) {
            if (_isEphemeral == true)
                return Task.CompletedTask;
            return GetMessageAsync().DelayedDeleteAsync(delay);
        }
    }

    public static class SentMessageExtensions {
        /// <returns>
        /// Original <paramref name="messageTask"/>
        /// </returns>
        public static Task CleanupAfter(this Task<SentMessage> messageTask, TimeSpan span) {
            _ = messageTask.PipeAsync(message => message.CleanupAfter(span));
            return messageTask;
        }
    }
}