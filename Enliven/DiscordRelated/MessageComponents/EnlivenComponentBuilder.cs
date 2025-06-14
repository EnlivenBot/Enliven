using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Handlers;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.MessageComponents;

/// <remarks>
/// Should be used one per message!
/// </remarks>
public class EnlivenComponentBuilder(MessageComponentInteractionsHandler messageComponentInteractionsHandler)
    : IDisposable {
    private readonly Dictionary<string, (DateTime, EnlivenButtonBuilder)> _entries = new();
    private CompositeDisposable? _buttonCallbackDisposables;
    private Func<ComponentBuilderCallback, ValueTask>? _commonCallback;

    public IReadOnlyDictionary<string, EnlivenButtonBuilder> Entries =>
        _entries.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Item2);

    public void Dispose() {
        _buttonCallbackDisposables?.Dispose();
    }

    public EnlivenComponentBuilder WithButton(EnlivenButtonBuilder button) {
        _entries[button.CustomId] = (DateTime.Now, button);
        return this;
    }

    public EnlivenComponentBuilder WithButton(string id, EnlivenButtonBuilder button) {
        _entries[id] = (DateTime.Now, button);
        return this;
    }

    public EnlivenButtonBuilder? GetButton(string id) {
        return TryGetButton(id, out var builder) ? builder : null;
    }

    public bool TryGetButton(string id, [NotNullWhen(true)] out EnlivenButtonBuilder? buttonBuilder) {
        buttonBuilder = null;
        if (!_entries.TryGetValue(id, out var tuple)) return false;
        buttonBuilder = tuple.Item2;
        return true;
    }

    public EnlivenButtonBuilder GetOrAddButton(string id) {
        return GetOrAddButton(id, s => new EnlivenButtonBuilder());
    }

    public EnlivenButtonBuilder GetOrAddButton(string id, Func<string, EnlivenButtonBuilder> factory) {
        if (_entries.TryGetValue(id, out var tuple)) return tuple.Item2;
        var enlivenButtonBuilder = factory(id);
        _entries[id] = (DateTime.Now, enlivenButtonBuilder);
        return enlivenButtonBuilder;
    }

    public EnlivenComponentBuilder RemoveButton(string id) {
        _entries.Remove(id);
        return this;
    }

    public EnlivenComponentBuilder SetCallback(Func<ComponentBuilderCallback, ValueTask>? callback) {
        _commonCallback = callback;
        return this;
    }

    private async ValueTask OnCurrentMessageCallbackTriggered(IInteractionContext context) {
        if (_commonCallback is null) return;
        if (context.Interaction is not IComponentInteraction componentInteraction) return;
        if (!componentInteraction.Data.CustomId.EndsWith('|')) return;
        var actualId = componentInteraction.Data.CustomId[..^37];
        if (_entries.TryGetValue(actualId, out var tuple))
            await _commonCallback(new ComponentBuilderCallback(actualId, context, tuple.Item2));
    }

    /// <remarks>
    /// Invalidates previous <see cref="Build"/> button's callbacks
    /// </remarks>
    public MessageComponent Build() {
        _buttonCallbackDisposables?.Dispose();
        _buttonCallbackDisposables = new CompositeDisposable();
        var builder = new ComponentBuilder();
        var rows = _entries
            .Where(pair => pair.Value.Item2.IsVisible)
            .GroupBy(pair => pair.Value.Item2.TargetRow)
            .OrderBy(pairs => pairs.Key)
            .Select((pairs, i) => new { Row = Math.Min(pairs.Key, i), Values = pairs });
        foreach (var pairs in rows) {
            var builders = pairs.Values
                .OrderBy(pair => pair.Value.Item2.Priority ?? 0)
                .ThenBy(pair => pair.Value.Item1)
                .Take(5);
            foreach (var (_, (_, buttonBuilder)) in builders) {
                var userCustomId = buttonBuilder.CustomId;
                var systemCustomId = $"{userCustomId}{buttonBuilder.Guid}|";
                builder.WithButton(buttonBuilder.WithCustomId(systemCustomId), pairs.Row);
                buttonBuilder.CustomId = userCustomId;

                _buttonCallbackDisposables.Add(messageComponentInteractionsHandler.RegisterMessageComponent(
                    systemCustomId, OnCurrentMessageCallbackTriggered));
                if (buttonBuilder.Callback != null) {
                    _buttonCallbackDisposables.Add(
                        messageComponentInteractionsHandler.RegisterMessageComponent(
                            systemCustomId, buttonBuilder.Callback));
                }
            }
        }

        return builder.Build();
    }

    public class ComponentBuilderCallback(
        string customId,
        IInteractionContext context,
        EnlivenButtonBuilder builder) {
        public string CustomId { get; } = customId;
        public IInteractionContext Context { get; } = context;

        public IComponentInteraction Interaction { get; } =
            context.Interaction as IComponentInteraction
            ?? throw new InvalidOperationException("Interaction is not a component interaction");

        public EnlivenButtonBuilder Builder { get; } = builder;
    }
}