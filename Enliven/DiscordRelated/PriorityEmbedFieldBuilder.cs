using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Discord;

namespace Bot.DiscordRelated {
    public sealed class PriorityEmbedFieldBuilder : EmbedFieldBuilder {
        private BehaviorSubject<int?> _priority = new BehaviorSubject<int?>(null);
        private BehaviorSubject<bool> _isEnabled = new BehaviorSubject<bool>(true);

        public IObservable<int?> PriorityChanged => _priority.AsObservable();

        public int? Priority {
            get => _priority.Value;
            set => _priority.OnNext(value);
        }

        public IObservable<bool> IsEnabledChanged => _isEnabled.AsObservable();

        public bool IsEnabled {
            get => _isEnabled.Value;
            set => _isEnabled.OnNext(value);
        }

        public PriorityEmbedFieldBuilder ClearPriority() {
            return WithPriority(null);
        }

        public PriorityEmbedFieldBuilder WithPriority(int? newPriority) {
            Priority = newPriority;
            return this;
        }

        public PriorityEmbedFieldBuilder WithEnabled(bool enabled) {
            IsEnabled = enabled;
            return this;
        }

        /// <summary>Sets the field name.</summary>
        /// <param name="name">The name to set the field name to.</param>
        /// <returns>The current builder.</returns>
        public new PriorityEmbedFieldBuilder WithName(string name) {
            return (PriorityEmbedFieldBuilder) base.WithName(name);
        }

        /// <summary>Sets the field value.</summary>
        /// <param name="value">The value to set the field value to.</param>
        /// <returns>The current builder.</returns>
        public new PriorityEmbedFieldBuilder WithValue(object value) {
            return (PriorityEmbedFieldBuilder) base.WithValue(value);
        }

        /// <summary>
        ///     Determines whether the field should be in-line with each other.
        /// </summary>
        /// <returns>The current builder.</returns>
        public new PriorityEmbedFieldBuilder WithIsInline(bool isInline) {
            return (PriorityEmbedFieldBuilder) base.WithIsInline(isInline);
        }
    }
}