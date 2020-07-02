using System;
using System.Linq;
using Discord;
using Hellosam.Net.Collections;

#pragma warning disable 8605
#pragma warning disable 618
#pragma warning disable 8606

// ReSharper disable InvalidXmlDocComment

namespace Bot.Utilities {
    public class PriorityEmbedBuilderWrapper {
        private readonly EmbedBuilder _embedBuilder = new EmbedBuilder();

        private readonly ObservableDictionary<string, PriorityEmbedFieldBuilder> _priorityFields =
            new ObservableDictionary<string, PriorityEmbedFieldBuilder>();

        public PriorityEmbedBuilderWrapper() {
            _priorityFields.CollectionChanged += (sender, args) => {
                if (args.NewItems != null) {
                    foreach (PriorityEmbedFieldBuilder builder in args.NewItems) {
                        try {
                            builder!.AddTime = DateTime.Now;
                            builder!.EnabledChanged += BuilderOnEnabledChanged;
                            builder!.PriorityChanged += BuilderOnPriorityChanged;
                        }
                        catch (Exception) {
                            // ignored
                        }
                    }
                }

                if (args.OldItems != null) {
                    foreach (PriorityEmbedFieldBuilder builder in args.OldItems) {
                        try {
                            builder!.EnabledChanged -= BuilderOnEnabledChanged;
                            builder!.PriorityChanged -= BuilderOnPriorityChanged;
                        }
                        catch (Exception) {
                            // ignored
                        }
                    }
                }

                UpdateEmbedFieldsInternal();
            };
        }

        private void BuilderOnPriorityChanged(object? sender, int? e) {
            UpdateEmbedFieldsInternal();
        }

        private void BuilderOnEnabledChanged(object? sender, bool e) {
            UpdateEmbedFieldsInternal();
        }

        /// <summary> Gets or sets the title of an <see cref="Embed"/>. </summary>
        /// <exception cref="ArgumentException" accessor="set">Title length exceeds <see cref="MaxTitleLength"/>.
        /// </exception>
        /// <returns> The title of the embed.</returns>
        public string Title {
            get => _embedBuilder.Title;
            set => _embedBuilder.Title = value;
        }

        /// <summary> Gets or sets the description of an <see cref="Embed"/>. </summary>
        /// <exception cref="ArgumentException" accessor="set">Description length exceeds <see cref="MaxDescriptionLength"/>.</exception>
        /// <returns> The description of the embed.</returns>
        public string Description {
            get => _embedBuilder.Description;
            set => _embedBuilder.Description = value;
        }

        /// <summary> Gets or sets the URL of an <see cref="Embed"/>. </summary>
        /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri"/>.</exception>
        /// <returns> The URL of the embed.</returns>
        public string Url {
            get => _embedBuilder.Url;
            set => _embedBuilder.Url = value;
        }

        /// <summary> Gets or sets the thumbnail URL of an <see cref="Embed"/>. </summary>
        /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri"/>.</exception>
        /// <returns> The thumbnail URL of the embed.</returns>
        public string ThumbnailUrl {
            get => _embedBuilder.ThumbnailUrl;
            set => _embedBuilder.ThumbnailUrl = value;
        }

        /// <summary> Gets or sets the image URL of an <see cref="Embed"/>. </summary>
        /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri"/>.</exception>
        /// <returns> The image URL of the embed.</returns>
        public string ImageUrl {
            get => _embedBuilder.ImageUrl;
            set => _embedBuilder.ImageUrl = value;
        }

        /// <summary> Gets or sets the list of <see cref="EmbedFieldBuilder"/> of an <see cref="Embed"/>. </summary>
        /// <exception cref="ArgumentNullException" accessor="set">An embed builder's fields collection is set to 
        /// <c>null</c>.</exception>
        /// <exception cref="ArgumentException" accessor="set">Description length exceeds <see cref="MaxFieldCount"/>.
        /// </exception>
        /// <returns> The list of existing <see cref="EmbedFieldBuilder"/>.</returns>
        public ObservableDictionary<string, PriorityEmbedFieldBuilder> Fields => _priorityFields;

        /// <summary>
        ///     Gets or sets the timestamp of an <see cref="Embed"/>.
        /// </summary>
        /// <returns>
        ///     The timestamp of the embed, or <c>null</c> if none is set.
        /// </returns>
        public DateTimeOffset? Timestamp {
            get => _embedBuilder.Timestamp;
            set => _embedBuilder.Timestamp = value;
        }

        /// <summary>
        ///     Gets or sets the sidebar color of an <see cref="Embed"/>.
        /// </summary>
        /// <returns>
        ///     The color of the embed, or <c>null</c> if none is set.
        /// </returns>
        public Color? Color {
            get => _embedBuilder.Color;
            set => _embedBuilder.Color = value;
        }

        /// <summary>
        ///     Gets or sets the <see cref="EmbedAuthorBuilder" /> of an <see cref="Embed"/>.
        /// </summary>
        /// <returns>
        ///     The author field builder of the embed, or <c>null</c> if none is set.
        /// </returns>
        public EmbedAuthorBuilder Author {
            get => _embedBuilder.Author;
            set => _embedBuilder.Author = value;
        }

        /// <summary>
        ///     Gets or sets the <see cref="EmbedFooterBuilder" /> of an <see cref="Embed"/>.
        /// </summary>
        /// <returns>
        ///     The footer field builder of the embed, or <c>null</c> if none is set.
        /// </returns>
        public EmbedFooterBuilder Footer {
            get => _embedBuilder.Footer;
            set => _embedBuilder.Footer = value;
        }

        /// <summary>
        ///     Gets the total length of all embed properties.
        /// </summary>
        /// <returns>
        ///     The combined length of <see cref="Title"/>, <see cref="EmbedAuthor.Name"/>, <see cref="Description"/>, 
        ///     <see cref="EmbedFooter.Text"/>, <see cref="EmbedField.Name"/>, and <see cref="EmbedField.Value"/>.
        /// </returns>
        public int Length => _embedBuilder.Length;

        /// <summary>
        ///     Sets the title of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="title">The title to be set.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithTitle(string title) {
            Title = title;
            return this;
        }

        /// <summary> 
        ///     Sets the description of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="description"> The description to be set. </param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithDescription(string description) {
            Description = description;
            return this;
        }

        /// <summary> 
        ///     Sets the URL of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="url"> The URL to be set. </param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithUrl(string url) {
            Url = url;
            return this;
        }

        /// <summary> 
        ///     Sets the thumbnail URL of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="thumbnailUrl"> The thumbnail URL to be set. </param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithThumbnailUrl(string thumbnailUrl) {
            ThumbnailUrl = thumbnailUrl;
            return this;
        }

        /// <summary>
        ///     Sets the image URL of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="imageUrl">The image URL to be set.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithImageUrl(string imageUrl) {
            ImageUrl = imageUrl;
            return this;
        }

        /// <summary>
        ///     Sets the timestamp of an <see cref="Embed" /> to the current time.
        /// </summary>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithCurrentTimestamp() {
            Timestamp = DateTimeOffset.UtcNow;
            return this;
        }

        /// <summary>
        ///     Sets the timestamp of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="dateTimeOffset">The timestamp to be set.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithTimestamp(DateTimeOffset dateTimeOffset) {
            Timestamp = dateTimeOffset;
            return this;
        }

        /// <summary>
        ///     Sets the sidebar color of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="color">The color to be set.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithColor(Color color) {
            Color = color;
            return this;
        }

        /// <summary>
        ///     Sets the <see cref="EmbedAuthorBuilder" /> of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="author">The author builder class containing the author field properties.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithAuthor(EmbedAuthorBuilder author) {
            Author = author;
            return this;
        }

        /// <summary>
        ///     Sets the author field of an <see cref="Embed" /> with the provided properties.
        /// </summary>
        /// <param name="action">The delegate containing the author field properties.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithAuthor(Action<EmbedAuthorBuilder> action) {
            var author = new EmbedAuthorBuilder();
            action(author);
            Author = author;
            return this;
        }

        /// <summary>
        ///     Sets the author field of an <see cref="Embed" /> with the provided name, icon URL, and URL.
        /// </summary>
        /// <param name="name">The title of the author field.</param>
        /// <param name="iconUrl">The icon URL of the author field.</param>
        /// <param name="url">The URL of the author field.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithAuthor(string name, string iconUrl = null, string url = null) {
            var author = new EmbedAuthorBuilder {
                Name = name,
                IconUrl = iconUrl,
                Url = url
            };
            Author = author;
            return this;
        }

        /// <summary>
        ///     Sets the <see cref="EmbedFooterBuilder" /> of an <see cref="Embed"/>.
        /// </summary>
        /// <param name="footer">The footer builder class containing the footer field properties.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithFooter(EmbedFooterBuilder footer) {
            Footer = footer;
            return this;
        }

        /// <summary>
        ///     Sets the footer field of an <see cref="Embed" /> with the provided properties.
        /// </summary>
        /// <param name="action">The delegate containing the footer field properties.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithFooter(Action<EmbedFooterBuilder> action) {
            var footer = new EmbedFooterBuilder();
            action(footer);
            Footer = footer;
            return this;
        }

        /// <summary>
        ///     Sets the footer field of an <see cref="Embed" /> with the provided name, icon URL.
        /// </summary>
        /// <param name="text">The title of the footer field.</param>
        /// <param name="iconUrl">The icon URL of the footer field.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper WithFooter(string text, string iconUrl = null) {
            var footer = new EmbedFooterBuilder {
                Text = text,
                IconUrl = iconUrl
            };
            Footer = footer;
            return this;
        }

        /// <summary>
        ///     Adds an <see cref="Embed" /> field with the provided name and value.
        /// </summary>
        /// <param name="name">The title of the field.</param>
        /// <param name="value">The value of the field.</param>
        /// <param name="inline">Indicates whether the field is in-line or not.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper AddField(string id, string name, object value, bool inline = false, int? priority = null, bool isEnabled = true) {
            var field = new PriorityEmbedFieldBuilder()
                       .WithIsInline(inline)
                       .WithName(name)
                       .WithValue(value)
                       .WithPriority(priority)
                       .WithEnabled(isEnabled);
            AddField(id, field);
            return this;
        }

        /// <summary>
        ///     Adds a field with the provided <see cref="EmbedFieldBuilder" /> to an
        ///     <see cref="Embed"/>.
        /// </summary>
        /// <param name="field">The field builder class containing the field properties.</param>
        /// <exception cref="ArgumentException">Field count exceeds <see cref="MaxFieldCount"/>.</exception>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper AddField(string id, PriorityEmbedFieldBuilder field) {
            _priorityFields.Add(id, field);
            return this;
        }

        /// <summary>
        ///     Adds an <see cref="Embed" /> field with the provided properties.
        /// </summary>
        /// <param name="action">The delegate containing the field properties.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public PriorityEmbedBuilderWrapper AddField(string id, Action<PriorityEmbedFieldBuilder> action) {
            var field = new PriorityEmbedFieldBuilder();
            action(field);
            AddField(id, field);
            return this;
        }

        public PriorityEmbedFieldBuilder GetOrAddField(string id) {
            return GetOrAddField(id, s => new PriorityEmbedFieldBuilder());
        }

        public PriorityEmbedFieldBuilder GetOrAddField(string id, Func<string, PriorityEmbedFieldBuilder> createFieldBuilder) {
            if (Fields.TryGetValue(id, out var fieldBuilder)) return fieldBuilder;
            fieldBuilder = createFieldBuilder(id);
            Fields.Add(id, fieldBuilder);

            return fieldBuilder;
        }

        /// <summary>
        ///     Builds the <see cref="Embed" /> into a Rich Embed ready to be sent.
        /// </summary>
        /// <returns>
        ///     The built embed object.
        /// </returns>
        /// <exception cref="InvalidOperationException">Total embed length exceeds <see cref="MaxEmbedLength"/>.</exception>
        public Embed Build() {
            return _embedBuilder.Build();
        }

        private void UpdateEmbedFieldsInternal() {
            _embedBuilder.Fields.Clear();
            _embedBuilder.Fields.AddRange(_priorityFields.Values.Where(builder => builder.IsEnabled)
                                                         .OrderBy(builder => builder.Priority ?? 0).ThenBy(builder => builder.AddTime));
        }
    }

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