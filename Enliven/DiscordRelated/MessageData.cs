using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated {
    public interface IMessageData : IMessageSendData {
        public string Text { get; }
        public Embed Embed { get; }
        public MessageComponent? Component { get; }
        public MessageReference? ReferencedMessage { get; }
        public AllowedMentions? AllowedMentions { get; }
        public string? Filename { get; }
        public Stream? File { get; }

        public new async Task<IUserMessage> SendMessage(IMessageChannel targetChannel) {
            if (File != null) {
                return await targetChannel.SendFileAsync(File, Filename, Text, embed: Embed, messageReference: ReferencedMessage, allowedMentions: AllowedMentions, component: Component);
            }
            else {
                return await targetChannel.SendMessageAsync(Text, embed: Embed, messageReference: ReferencedMessage, allowedMentions: AllowedMentions, component: Component);
            }
        }
    }

    public interface IMessageSendData {
        public Task<IUserMessage> SendMessage(IMessageChannel targetChannel);
    }
}