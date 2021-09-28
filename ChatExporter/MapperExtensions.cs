using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatExporter.Core.Discord.Data.Common;
using DiscordChatExporter.Core.Utils.Extensions;
using NLog;
using Tyrrrz.Extensions;
using Attachment = DiscordChatExporter.Core.Discord.Data.Attachment;
using EmbedAuthor = DiscordChatExporter.Core.Discord.Data.Embeds.EmbedAuthor;
using EmbedField = DiscordChatExporter.Core.Discord.Data.Embeds.EmbedField;
using EmbedFooter = DiscordChatExporter.Core.Discord.Data.Embeds.EmbedFooter;
using EmbedImage = DiscordChatExporter.Core.Discord.Data.Embeds.EmbedImage;
using Embed = DiscordChatExporter.Core.Discord.Data.Embeds.Embed;
using Emoji = DiscordChatExporter.Core.Discord.Data.Emoji;
using MessageReference = DiscordChatExporter.Core.Discord.Data.MessageReference;

namespace ChatExporter {
    internal static class MapperExtensions {
        private static ILogger Logger = LogManager.GetCurrentClassLogger();
        
        public static User ToUser(this IUser user) =>
            new User(new Snowflake(user.Id), user.IsBot, user.DiscriminatorValue, user.Username, user.GetAvatarUrl());

        public static Role ToRole(this IRole role) =>
            new Role(new Snowflake(role.Id), role.Name, role.Position, role.Color);

        public static Guild ToGuild(this IGuild guild) =>
            new Guild(new Snowflake(guild.Id), guild.Name, guild.IconUrl);

        private static ChannelCategory GetFallbackCategory(ChannelKind channelKind) {
            var name = channelKind switch {
                ChannelKind.GuildTextChat       => "Text",
                ChannelKind.DirectTextChat      => "Private",
                ChannelKind.DirectGroupTextChat => "Group",
                ChannelKind.GuildNews           => "News",
                ChannelKind.GuildStore          => "Store",
                _                               => "Default"
            };
            return new ChannelCategory(Snowflake.Zero, name, null);
        }

        public static ChannelCategory ToChannelCategory(this ICategoryChannel categoryChannel) =>
            new ChannelCategory(new Snowflake(categoryChannel.Id), categoryChannel.Name, categoryChannel.Position);

        public static ChannelKind ToChannelKind(this IChannel channel) =>
            channel switch {
                ICategoryChannel _ => ChannelKind.GuildCategory,
                IDMChannel _       => ChannelKind.DirectTextChat,
                IGroupChannel _    => ChannelKind.DirectGroupTextChat,
                INewsChannel _     => ChannelKind.GuildNews,
                IVoiceChannel _    => ChannelKind.GuildVoiceChat,
                ITextChannel _     => ChannelKind.GuildTextChat,
                _                  => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
            };

        private static async Task<ChannelCategory> GetCategory(IChannel channel, ChannelKind kind) =>
            channel is INestedChannel nestedChannel ? ToChannelCategory(await nestedChannel.GetCategoryAsync()) : GetFallbackCategory(kind);

        public static async Task<Channel> ToChannel(this IChannel channel) {
            var kind = ToChannelKind(channel);
            var guildId = new Snowflake((channel as IGuildChannel)?.GuildId ?? 0UL);
            var channelCategory = await GetCategory(channel, kind);
            var id = new Snowflake(channel.Id);
            var position = (channel as IGuildChannel)?.Position;
            var topic = (channel as ITextChannel)?.Topic;

            return new Channel(id, kind, guildId, channelCategory, channel.Name, position, topic);
        }

        public static Member ToMember(this IGuildUser member) =>
            new Member(ToUser(member), member.Nickname, member.RoleIds.Select(item => new Snowflake(item)).ToList());

        public static string GetEmojiImageUrl(string? id, string name, bool isAnimated)
        {
            // Custom emoji
            if (!string.IsNullOrWhiteSpace(id)) {
                return isAnimated
                    ? $"https://cdn.discordapp.com/emojis/{id}.gif"
                    : $"https://cdn.discordapp.com/emojis/{id}.png";
            }

            // Standard emoji
            var twemojiName = name
                .GetRunes()
                // Variant selector rune is skipped in Twemoji names
                .Where(r => r.Value != 0xfe0f)
                .Select(r => r.Value.ToString("x"))
                .JoinToString("-");
            return $"https://twemoji.maxcdn.com/2/svg/{twemojiName}.svg";
        }
        
        public static Emoji ToEmoji(this IEmote emote) {
            var id = (emote as Emote)?.Id.ToString();
            var isAnimated = (emote as Emote)?.Animated ?? false;
            var imageUrl = GetEmojiImageUrl(id, emote.Name, isAnimated);
            return new Emoji(id, emote.Name, isAnimated, imageUrl);
        }

        public static Reaction ToReaction(this IEmote emoji, ReactionMetadata reactionMetadata) =>
            new Reaction(emoji.ToEmoji(), reactionMetadata.ReactionCount);
        
        public static Attachment ToAttachment(this IAttachment attachment) =>
            new Attachment(new Snowflake(attachment.Id), attachment.Url, attachment.Filename, attachment.Width, attachment.Height, new FileSize(attachment.Size));

        public static MessageKind ToMessageKind(this MessageType messageType) =>
            messageType switch {
                MessageType.Default              => MessageKind.Default,
                MessageType.RecipientAdd         => MessageKind.RecipientAdd,
                MessageType.RecipientRemove      => MessageKind.RecipientRemove,
                MessageType.Call                 => MessageKind.Call,
                MessageType.ChannelNameChange    => MessageKind.ChannelNameChange,
                MessageType.ChannelIconChange    => MessageKind.ChannelIconChange,
                MessageType.ChannelPinnedMessage => MessageKind.ChannelPinnedMessage,
                MessageType.GuildMemberJoin      => MessageKind.GuildMemberJoin,
                MessageType.Reply                => MessageKind.Reply,
                _                                => MessageKind.Default
            };

        public static EmbedAuthor ToEmbedAuthor(this Discord.EmbedAuthor embedAuthor) =>
            new EmbedAuthor(embedAuthor.Name, embedAuthor.Url, embedAuthor.IconUrl, embedAuthor.ProxyIconUrl);

        public static EmbedField ToEmbedField(Discord.EmbedField embedField) =>
            new EmbedField(embedField.Name, embedField.Value, embedField.Inline);

        public static EmbedFooter ToEmbedFooter(Discord.EmbedFooter embedFooter) =>
            new EmbedFooter(embedFooter.Text, embedFooter.IconUrl, embedFooter.ProxyUrl);

        public static EmbedImage ToEmbedImage(Discord.EmbedImage embedImage) =>
            new EmbedImage(embedImage.Url, embedImage.ProxyUrl, embedImage.Width, embedImage.Height);
        
        public static EmbedImage ToEmbedImage(EmbedThumbnail embedThumbnail) =>
            new EmbedImage(embedThumbnail.Url, embedThumbnail.ProxyUrl, embedThumbnail.Width, embedThumbnail.Height);

        public static Embed ToEmbed(IEmbed embed) {
            var embedAuthor = embed.Author != null ? ToEmbedAuthor((Discord.EmbedAuthor) embed.Author) : null;
            var fields = embed.Fields.Select(ToEmbedField).ToList();
            var thumbnail = embed.Thumbnail != null ? ToEmbedImage((EmbedThumbnail) embed.Thumbnail) : null;
            var image = embed.Image != null ? ToEmbedImage((Discord.EmbedImage) embed.Image) : null;
            var footer = embed.Footer != null ? ToEmbedFooter((Discord.EmbedFooter) embed.Footer) : null;
            return new Embed(embed.Title, embed.Url, embed.Timestamp, embed.Color, embedAuthor, embed.Description, fields, thumbnail, image, footer);
        }

        public static MessageReference ToMessageReference(Discord.MessageReference messageReference) =>
            new MessageReference(new Snowflake(messageReference.MessageId.GetValueOrDefault(0)), new Snowflake(messageReference.ChannelId), new Snowflake((ulong) messageReference.GuildId));

        public static Message ToMessage(this IMessage message) {
            var attachments = message.Attachments.Select(ToAttachment).ToList();
            var embeds = message.Embeds.Select(ToEmbed).ToList();
            var reactions = message.Reactions.Select(item => item.Key.ToReaction(item.Value)).ToList();
            var messageReference = message.Reference != null ? ToMessageReference(message.Reference) : null;
            var mentionedUsers = GetMentionedUsers(message);
            var referencedMessage = GetReferencedMessage(message);
            return new Message(new Snowflake(message.Id), message.Type.ToMessageKind(), message.Author.ToUser(),
                message.Timestamp, message.EditedTimestamp,
                null, // Calls available only in user chats, bot doesnt need this
                message.IsPinned, message.Content, attachments, embeds, reactions,
                mentionedUsers, messageReference, referencedMessage);
        }

        [SuppressMessage("ReSharper", "RedundantEnumerableCastCall")]
        private static List<User> GetMentionedUsers(IMessage message) {
            try {
                return (message switch {
                    RestMessage restMessage     => restMessage.MentionedUsers.Cast<IUser>(),
                    SocketMessage socketMessage => socketMessage.MentionedUsers.Cast<IUser>(),
                    _                           => throw new ArgumentOutOfRangeException(nameof(message))
                }).Select(user => user.ToUser()).ToList();
            }
            catch (ArgumentOutOfRangeException e) {
                Logger.Error(e, "Cannot get MentionedUsers from IMessage");
                return new List<User>();
            }
        }
        
        [SuppressMessage("ReSharper", "RedundantEnumerableCastCall")]
        private static Message? GetReferencedMessage(IMessage message) {
            try {
                return (message switch {
                    RestFollowupMessage restFollowupMessage       => restFollowupMessage.ReferencedMessage,
                    RestInteractionMessage restInteractionMessage => restInteractionMessage.ReferencedMessage,
                    RestSystemMessage restSystemMessage           => null,
                    RestUserMessage restUserMessage               => restUserMessage.ReferencedMessage,
                    RestMessage restMessage                       => null,
                    SocketSystemMessage socketSystemMessage       => null,
                    SocketUserMessage socketUserMessage           => socketUserMessage.ReferencedMessage,
                    SocketMessage socketMessage                   => null,
                    _                                             => throw new ArgumentOutOfRangeException(nameof(message))
                })?.ToMessage() ?? null;
            }
            catch (ArgumentOutOfRangeException e) {
                Logger.Error(e, "Cannot get ReferencedMessage from IMessage");
                return null;
            }
        }
    }
}