using System;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.MessageComponents {
    public interface IEnlivenComponentBuilder {
        List<ActionRowBuilder> ActionRows { get; set; }
        MessageComponent Build();
        ComponentBuilder WithButton(string label, string customId, Action<SocketMessageComponent>? callback = null, ButtonStyle style = ButtonStyle.Primary, IEmote emote = null, string url = null, bool disabled = false, int row = 0);
        ComponentBuilder WithButton(ButtonBuilder button, Action<SocketMessageComponent>? callback = null);
        ComponentBuilder WithButton(ButtonBuilder button, int row, Action<SocketMessageComponent>? callback = null);
    }
}