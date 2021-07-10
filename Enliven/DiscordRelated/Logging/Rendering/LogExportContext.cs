using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Discord;
using Discord.WebSocket;
using DiscordChatExporter.Domain.Discord.Models;
using DiscordChatExporter.Domain.Discord.Models.Common;
using DiscordChatExporter.Domain.Exporting;
using ChannelType = DiscordChatExporter.Domain.Discord.Models.ChannelType;

namespace Bot.DiscordRelated.Logging.Rendering {
    public class LogExportContext : ExportContext {
        public LogExportContext(Guild guild, Channel channel, DateTimeOffset? after, DateTimeOffset? before, string dateFormat,
                                IReadOnlyCollection<Member> members, IReadOnlyCollection<Channel> channels, IReadOnlyCollection<Role> roles) : base(guild,
            channel, after, before, dateFormat, members, channels, roles) { }

        public static LogExportContext Create(ulong channelId, out HashSet<Member> members) {
            var channel = (ITextChannel) EnlivenBot.Client.GetChannel(channelId);
            var guild = (SocketGuild) channel.Guild;
            
            members = new HashSet<Member>(IdBasedEqualityComparer.Instance);
            var context = new LogExportContext(new Guild(guild.Id.ToString(), guild.Name, null), 
                new Channel(channel.Id.ToString(), ChannelType.GuildTextChat, guild.Id.ToString(), "Unspecified",channel.Name, channel.Topic), 
                null, null, "dd-MMM-yy hh:mm tt", members,  
                new ReadOnlyCollection<Channel>(guild.TextChannels.Select(discordChannel => new Channel(discordChannel.Id.ToString(), ChannelType.GuildTextChat,
                    guild.Id.ToString(), "Unspecified", discordChannel.Name, discordChannel.Topic)).ToList()),
                new ReadOnlyCollection<Role>(guild.Roles.Select(discordRole => new Role(discordRole.Id.ToString(), discordRole.Name, discordRole.Position, discordRole.Color)).ToList()));
            return context;
        }
    }
}