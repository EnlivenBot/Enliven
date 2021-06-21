using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.MessageComponents {
    /// <remarks>
    /// Should be used one per message!
    /// </remarks>
    public class EnlivenComponentManager : IDisposable {
        private readonly MessageComponentService _messageComponentService;
        private readonly Dictionary<string, (DateTime, EnlivenButtonBuilder)> _entries = new Dictionary<string, (DateTime, EnlivenButtonBuilder)>();
        private CompositeDisposable? buttonCallbackDisposables;
        private IUserMessage? _message;
        private Action<string, SocketMessageComponent, EnlivenButtonBuilder>? _callback;
        private IDisposable? _callbackDisposable;

        public IReadOnlyDictionary<string, EnlivenButtonBuilder> Entries => _entries.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Item2);

        public EnlivenComponentManager(MessageComponentService messageComponentService) {
            _messageComponentService = messageComponentService;
        }

        public EnlivenComponentManager WithButton(EnlivenButtonBuilder button) {
            _entries[button.CustomId] = (DateTime.Now, button);
            return this;
        }

        public EnlivenComponentManager WithButton(string id, EnlivenButtonBuilder button) {
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
            if (_entries.TryGetValue(id, out var tuple)) {
                return tuple.Item2;
            }
            var enlivenButtonBuilder = factory(id);
            _entries[id] = (DateTime.Now, enlivenButtonBuilder);
            return enlivenButtonBuilder;
        }

        public EnlivenComponentManager RemoveButton(string id) {
            _entries.Remove(id);
            return this;
        }

        public void AssociateWithMessage(IUserMessage? message) {
            _message = message;
            TrySetMessageCallback();
        }

        public void AssociateWithMessage(Task<IUserMessage> message) {
            message.ContinueWith(async task => AssociateWithMessage(await task), TaskContinuationOptions.NotOnFaulted);
        }

        public void SetCallback(Action<string, SocketMessageComponent, EnlivenButtonBuilder>? callback) {
            _callback = callback;
            TrySetMessageCallback();
        }

        private void TrySetMessageCallback() {
            _callbackDisposable?.Dispose();
            if (_callback != null && _message != null) {
                _callbackDisposable = _messageComponentService.MessageComponentUse
                    .Where(component => component.Message.Id == _message.Id & component.Channel.Id == _message.Channel.Id)
                    .Subscribe(OnCurrentMessageCallbackTriggered);
            }
        }

        private void OnCurrentMessageCallbackTriggered(SocketMessageComponent component) {
            if (!component.Data.CustomId.EndsWith("|")) return;
            var actualId = component.Data.CustomId[..^37];
            if (_entries.TryGetValue(actualId, out var tuple)) {
                Task.Run(() => _callback!(actualId, component, tuple.Item2));
            }
        }

        private Task UpdateAssociatedMessageComponents() {
            return _message?.ModifyAsync(properties => properties.Components = Build()) ?? Task.CompletedTask;
        }

        /// <remarks>
        /// Invalidates previous <see cref="Build"/> button's callbacks
        /// </remarks>
        public MessageComponent Build() {
            buttonCallbackDisposables?.Dispose();
            buttonCallbackDisposables = new CompositeDisposable();
            var builder = new ComponentBuilder();
            List<(DateTime, EnlivenButtonBuilder)> toWrap = new List<(DateTime, EnlivenButtonBuilder)>();
            foreach (var pairs in _entries.GroupBy(pair => pair.Value.Item2.TargetRow).OrderBy(pairs => pairs.Key)) {
                foreach (var (_, (_, buttonBuilder)) in pairs.Where(pair => pair.Value.Item2.IsVisible).OrderBy(pair => pair.Value.Item2.Priority ?? 0).ThenBy(pair => pair.Value.Item1).Take(5)) {
                    var userCustomId = buttonBuilder.CustomId;
                    var systemCustomId = $"{userCustomId}{buttonBuilder.Guid}|";
                    builder.WithButton(buttonBuilder.WithCustomId(systemCustomId), buttonBuilder.TargetRow);
                    buttonBuilder.CustomId = userCustomId;

                    //Register callback
                    if (buttonBuilder.Callback != null)
                        buttonCallbackDisposables.Add(_messageComponentService.RegisterMessageComponent(systemCustomId, buttonBuilder.Callback));
                }
            }

            return builder.Build();
        }

        public void Dispose() {
            buttonCallbackDisposables?.Dispose();
        }
    }
}