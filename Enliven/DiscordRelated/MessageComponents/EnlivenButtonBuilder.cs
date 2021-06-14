using System;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.MessageComponents {
    public class EnlivenButtonBuilder : ButtonBuilder {
        public Guid Guid { get; } = Guid.NewGuid();
        
        /// <summary>
        /// Is <see cref="IsVisible"/> is <code>false</code> then this button will not get into MessageComponents when calling <see cref="EnlivenComponentManager.Build"/>
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Target row for current button
        /// </summary>
        public int TargetRow { get; set; } = 0;
        
        /// <summary>
        /// The higher the priority, the closer this button will be to the beginning of the row
        /// </summary>
        public int? Priority { get; set; }
        
        /// <summary>
        /// This delegate would be called if user press this button
        /// </summary>
        public Action<SocketMessageComponent>? Callback { get; set; }

        public EnlivenButtonBuilder WithIsVisible(bool isVisible) {
            IsVisible = isVisible;
            return this;
        }
        
        public EnlivenButtonBuilder WithTargetRow(int targetRow) {
            TargetRow = targetRow;
            return this;
        }
        public EnlivenButtonBuilder WithPriority(int? priority) {
            Priority = priority;
            return this;
        }
        
        public EnlivenButtonBuilder WithCallback(Action<SocketMessageComponent>? callback) {
            Callback = callback;
            return this;
        }
    }
}