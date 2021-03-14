using System;
using Discord;

namespace Bot.DiscordRelated {
    public sealed class PriorityEmbedFieldBuilder : EmbedFieldBuilder {
        private int? _priority;
        private bool _isEnabled = true;

        public event EventHandler<int?>? PriorityChanged;

        public int? Priority {
            get => _priority;
            set {
                _priority = value;
                OnPriorityChanged(value);
            }
        }

        public event EventHandler<bool>? EnabledChanged;

        public bool IsEnabled {
            get => _isEnabled;
            set {
                _isEnabled = value;
                OnEnabledChanged(value);
            }
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

        private void OnPriorityChanged(int? e) {
            PriorityChanged?.Invoke(this, e);
        }

        private void OnEnabledChanged(bool e) {
            EnabledChanged?.Invoke(this, e);
        }

        [Obsolete("This is a system property, do not modify it!")]
        public DateTime AddTime { get; set; }
    }
}