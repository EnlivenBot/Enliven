using System;
using Common.Config;
using Common.Entities;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;

namespace ChatExporter.Exporter.MessageHistories {
    internal class MessageHistoryExportContext : ExportContext {
        private Func<ulong, Member?> _memberResolver;
        private Func<ulong, Channel?> _channelResolver;
        private Func<ulong, Role?> _roleResolver;
        public MessageHistory MessageHistory { get; }
        public MessageHistoryExportContext(MessageHistory messageHistory, bool willBeRenderedToImage,
                                           Func<ulong, Member?> memberResolver,
                                           Func<ulong, Channel?> channelResolver,
                                           Func<ulong, Role?> roleResolver) 
            : base(willBeRenderedToImage) {
            _roleResolver = roleResolver;
            _channelResolver = channelResolver;
            _memberResolver = memberResolver;
            MessageHistory = messageHistory;
        }
        
        public Member? TryGetMember(UserLink arg) {
            return _memberResolver(arg.UserId);
        }
        
        public Member? TryGetMember(Snowflake arg) {
            return _memberResolver(arg.Value);
        }
        
        public Channel? TryGetChannel(Snowflake arg) {
            return _channelResolver(arg.Value);
        }
        
        public Role? TryGetRole(Snowflake arg) {
            return _roleResolver(arg.Value);
        }
    }
}