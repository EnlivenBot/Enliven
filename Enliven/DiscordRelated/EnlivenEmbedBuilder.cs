using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using Common.Localization.Entries;
using Discord;
#pragma warning disable 8605
#pragma warning disable 618
#pragma warning disable 8606

// ReSharper disable InvalidXmlDocComment

namespace Bot.DiscordRelated;

public class EnlivenEmbedBuilder {
    private readonly EmbedBuilder _embedBuilder = new();
    private readonly object _lockObject = new();

    private readonly Dictionary<string, (DateTime, PriorityEmbedFieldBuilder, IDisposable)> _priorityFields = new();
    private bool _isFieldsUpdateRequired = true;

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

    /// <summary>
    /// Gets the list of <see cref="EmbedFieldBuilder"/> of an <see cref="Embed"/>.
    /// </summary>
    /// <returns> The list of existing <see cref="EmbedFieldBuilder"/>.</returns>
    public IReadOnlyDictionary<string, PriorityEmbedFieldBuilder> Fields => _priorityFields.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Item2);

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
    public int Length {
        get {
            lock (_lockObject) {
                UpdateEmbedFieldsInternal();
                return _embedBuilder.Length;
            }
        }
    }

    /// <summary>
    ///     Sets the title of an <see cref="Embed"/>.
    /// </summary>
    /// <param name="title">The title to be set.</param>
    /// <returns>
    ///     The current builder.
    /// </returns>
    public EnlivenEmbedBuilder WithTitle(string title) {
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
    public EnlivenEmbedBuilder WithDescription(string description) {
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
    public EnlivenEmbedBuilder WithUrl(string url) {
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
    public EnlivenEmbedBuilder WithThumbnailUrl(string thumbnailUrl) {
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
    public EnlivenEmbedBuilder WithImageUrl(string imageUrl) {
        ImageUrl = imageUrl;
        return this;
    }

    /// <summary>
    ///     Sets the timestamp of an <see cref="Embed" /> to the current time.
    /// </summary>
    /// <returns>
    ///     The current builder.
    /// </returns>
    public EnlivenEmbedBuilder WithCurrentTimestamp() {
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
    public EnlivenEmbedBuilder WithTimestamp(DateTimeOffset dateTimeOffset) {
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
    public EnlivenEmbedBuilder WithColor(Color color) {
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
    public EnlivenEmbedBuilder WithAuthor(EmbedAuthorBuilder author) {
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
    public EnlivenEmbedBuilder WithAuthor(Action<EmbedAuthorBuilder> action) {
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
    public EnlivenEmbedBuilder WithAuthor(string name, string? iconUrl = null, string? url = null) {
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
    public EnlivenEmbedBuilder WithFooter(EmbedFooterBuilder footer) {
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
    public EnlivenEmbedBuilder WithFooter(Action<EmbedFooterBuilder> action) {
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
    public EnlivenEmbedBuilder WithFooter(string text, string? iconUrl = null) {
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
    public EnlivenEmbedBuilder AddField(string? id, string name, object value, bool inline = false, int? priority = null, bool isEnabled = true) {
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
    public EnlivenEmbedBuilder AddField(string? id, PriorityEmbedFieldBuilder field) {
        id ??= Guid.NewGuid().ToString();
        _priorityFields.Add(id, (DateTime.Now, field, SubscribeToBuilderUpdates(field)));
        return this;
    }

    private IDisposable SubscribeToBuilderUpdates(PriorityEmbedFieldBuilder field) {
        return new CompositeDisposable(
            field.PriorityChanged.Subscribe(i => _isFieldsUpdateRequired = true),
            field.IsEnabledChanged.Subscribe(b => _isFieldsUpdateRequired = true));
    }

    /// <summary>
    ///     Adds an <see cref="Embed" /> field with the provided properties.
    /// </summary>
    /// <param name="action">The delegate containing the field properties.</param>
    /// <returns>
    ///     The current builder.
    /// </returns>
    public EnlivenEmbedBuilder AddField(string id, Action<PriorityEmbedFieldBuilder> action) {
        var field = new PriorityEmbedFieldBuilder();
        action(field);
        AddField(id, field);
        return this;
    }

    public PriorityEmbedFieldBuilder GetOrAddField(string id) {
        var asd = (IEntry)new EntryString("") + new EntryString("");
        return GetOrAddField(id, s => new PriorityEmbedFieldBuilder());
    }

    public PriorityEmbedFieldBuilder GetOrAddField(string id, Func<string, PriorityEmbedFieldBuilder> createFieldBuilder) {
        if (Fields.TryGetValue(id, out var fieldBuilder)) return fieldBuilder;
        fieldBuilder = createFieldBuilder(id);
        AddField(id, fieldBuilder);

        return fieldBuilder;
    }

    public EnlivenEmbedBuilder RemoveField(string id) {
        if (_priorityFields.Remove(id, out var tuple)) tuple.Item3.Dispose();
        return this;
    }

    /// <summary>
    ///     Builds the <see cref="Embed" /> into a Rich Embed ready to be sent.
    /// </summary>
    /// <returns>
    ///     The built embed object.
    /// </returns>
    /// <exception cref="InvalidOperationException">Total embed length exceeds <see cref="MaxEmbedLength"/>.</exception>
    public Embed Build() {
        lock (_lockObject) {
            UpdateEmbedFieldsInternal();
            return _embedBuilder.Build();
        }
    }

    private void UpdateEmbedFieldsInternal() {
        if (!_isFieldsUpdateRequired) return;
        _embedBuilder.Fields.Clear();
        var fieldBuilders = _priorityFields.Values.Where(builder => builder.Item2.IsEnabled)
            .OrderBy(builder => builder.Item2.Priority ?? 0)
            .ThenBy(builder => builder.Item1)
            .Select(tuple => tuple.Item2);
        _embedBuilder.Fields.AddRange(fieldBuilders);
        _isFieldsUpdateRequired = false;
    }

    public EnlivenEmbedBuilder Clone() {
        var builder = new EnlivenEmbedBuilder()
            .WithAuthor(Author)
            .WithDescription(Description)
            .WithFooter(Footer)
            .WithTitle(Title)
            .WithUrl(Url)
            .WithCurrentTimestamp()
            .WithImageUrl(ImageUrl)
            .WithThumbnailUrl(ThumbnailUrl);
        if (Color != null) builder.WithColor((Color)Color);
        if (Timestamp != null) builder.WithTimestamp((DateTimeOffset)Timestamp);

        _priorityFields
            .ToList()
            .ForEach(pair => builder.AddField(pair.Key, pair.Value.Item2));

        return builder;
    }
}