using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.MessageComponents {
    public class EnlivenComponentBuilder : IEnlivenComponentBuilder {
        private readonly ComponentBuilder _componentBuilder;
        private readonly MessageComponentService _messageComponentService;

        public EnlivenComponentBuilder(MessageComponentService messageComponentService) {
            _messageComponentService = messageComponentService;
            _componentBuilder = new ComponentBuilder();
        }

        public List<ActionRowBuilder> ActionRows {
            get => _componentBuilder.ActionRows;
            set => _componentBuilder.ActionRows = value;
        }

        private readonly Dictionary<string, Action<SocketMessageComponent>?> _callbacks = new Dictionary<string, Action<SocketMessageComponent>?>();

        public ComponentBuilder WithButton(string label, string customId, Action<SocketMessageComponent>? callback = null, ButtonStyle style = ButtonStyle.Primary, IEmote emote = null, string url = null, bool disabled = false, int row = 0) {
            if (string.IsNullOrEmpty(url) || style != ButtonStyle.Link) customId += Guid.NewGuid();
            if (string.IsNullOrEmpty(customId)) _callbacks.Add(customId, callback);
            return _componentBuilder.WithButton(label, customId, style, emote, url, disabled, row);
        }
        
        public ComponentBuilder WithButton(ButtonBuilder button, Action<SocketMessageComponent>? callback = null) {
            if (string.IsNullOrEmpty(button.Url) || button.Style != ButtonStyle.Link) button.CustomId += Guid.NewGuid();
            if (string.IsNullOrEmpty(button.CustomId)) _callbacks.Add(button.CustomId, callback);
            return _componentBuilder.WithButton(button);
        }
        
        public ComponentBuilder WithButton(ButtonBuilder button, int row, Action<SocketMessageComponent>? callback = null) {
            if (string.IsNullOrEmpty(button.Url) || button.Style != ButtonStyle.Link) button.CustomId += Guid.NewGuid();
            if (string.IsNullOrEmpty(button.CustomId)) _callbacks.Add(button.CustomId, callback);
            return _componentBuilder.WithButton(button, row);
        }
        
        public MessageComponent Build() {
            var messageComponent = _componentBuilder.Build();
            foreach (var buttonComponent in messageComponent.Components.SelectMany(component => component.Components).Where(component => !string.IsNullOrEmpty(component.CustomId))) {
                if (_callbacks.TryGetValue(buttonComponent.CustomId, out var callback) && callback != null) 
                    _messageComponentService.RegisterMessageComponent(buttonComponent.CustomId, callback);
            }
            return messageComponent;
        }
    }
}