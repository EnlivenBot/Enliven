using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using Common.Entities;
using Common.Localization.Providers;
using Discord;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Exporting.Writers.Html;
using NLog;
using Tyrrrz.Extensions;

namespace ChatExporter.Exporter.MessageHistories {
    public class MessageHistoryHtmlExporter {
        private EnlivenShardedClient _enlivenShardedClient;
        private ILogger _logger;
        public MessageHistoryHtmlExporter(EnlivenShardedClient enlivenShardedClient, ILogger logger) {
            _logger = logger;
            _enlivenShardedClient = enlivenShardedClient;
        }

        public async Task<string> ExportToHtml(MessageHistory messageHistory, bool willBeRenderedToImage, bool isDarkTheme = true,
                                               Optional<ITextChannel?> messageChannel = new(), Optional<IMessage?> existentMessage = new(), Optional<IGuild?> guild = new()) {
            try {
                var channelTask = GetChannelAsync(messageHistory, messageChannel);
                var messageTask = GetMessageAsync(messageHistory, existentMessage, channelTask);
                var guildTask = GetGuildAsync(guild, channelTask);
                StringBuilder builder = new StringBuilder();
                var exportContext = new MessageHistoryExportContext(messageHistory, willBeRenderedToImage, 
                    arg => MemberResolver(arg, guildTask),
                    arg => ChannelResolver(arg, messageHistory.ChannelId, channelTask),
                    arg => RoleResolver(arg, guildTask));

                var themeName = isDarkTheme ? "Dark" : "Light";
                var title = $"Message {messageHistory.MessageId} from {messageHistory.ChannelId} channel by {messageHistory.Author}";
                var stylesTemplateContext = new StylesTemplateContext(exportContext, themeName, title);
                var stylesRenderTask = StylesTemplate.RenderAsync(stylesTemplateContext);

                var messageHistoryHeaderTemplateContext = new MessageHistoryHeaderTemplateContext(exportContext, await channelTask, await messageTask);
                var headerRenderTask = MessageHistoryHeaderTemplate.RenderAsync(messageHistoryHeaderTemplateContext);

                var snapshotsRenderTasks = messageHistory.GetSnapshots(LangLocalizationProvider.EnglishLocalizationProvider)
                    .Select(snapshot => new MessageHistorySnapshotContext(exportContext, snapshot))
                    .Select(MessageHistorySnapshot.RenderAsync);

                var postambleTemplateContext = new PostambleTemplateContext(exportContext);
                var postambleRenderTask = PostambleTemplate.RenderAsync(postambleTemplateContext);

                builder.AppendLine(await stylesRenderTask);
                builder.AppendLine(await headerRenderTask);
                builder.AppendLine((await Task.WhenAll(snapshotsRenderTasks)).JoinToString(Environment.NewLine));
                builder.AppendLine(await postambleRenderTask);

                return builder.ToString();
            }
            catch (Exception e) {
                _logger.Error(e, "Error while exporting message history to html");
                throw;
            }
        }

        private async Task<IGuild?> GetGuildAsync(Optional<IGuild?> guild, Task<ITextChannel?> channelTask) {
            return guild.IsSpecified ? guild.Value : (await channelTask)?.Guild;
        }

        private static async Task<IMessage?> GetMessageAsync(MessageHistory messageHistory, Optional<IMessage?> existentMessage, Task<ITextChannel?> channelTask) {
            if (existentMessage.IsSpecified) return existentMessage.Value;
            var channel = await channelTask;
            if (channel == null) return null;
            return await channel.GetMessageAsync(messageHistory.MessageId);
        }
        
        private async Task<ITextChannel?> GetChannelAsync(MessageHistory messageHistory, Optional<ITextChannel?> messageChannel) {
            if (messageChannel.IsSpecified) return messageChannel.Value;
            return await _enlivenShardedClient.GetChannelAsync(messageHistory.ChannelId) as ITextChannel;
        }

        private Role? RoleResolver(ulong arg, Task<IGuild?> guildTask) {
            return guildTask.GetAwaiter().GetResult()?.GetRole(arg).ToRole();
        }
        
        private Channel? ChannelResolver(ulong @ulong, ulong messageHistoryChannelId, Task<ITextChannel?> channelTask) {
            if (messageHistoryChannelId == @ulong) {
                return channelTask.GetAwaiter().GetResult()?.ToChannel().GetAwaiter().GetResult();
            }

            return _enlivenShardedClient.GetChannelAsync(@ulong).GetAwaiter().GetResult()?.ToChannel().GetAwaiter().GetResult();
        }

        private Member? MemberResolver(ulong arg, Task<IGuild?> guildTask) {
            var guildUser = guildTask.GetAwaiter().GetResult()?.GetUserAsync(arg).GetAwaiter().GetResult();
            if (guildUser != null) {
                return guildUser.ToMember();
            }

            var user = _enlivenShardedClient.GetUserAsync(arg).GetAwaiter().GetResult()?.ToUser();
            return user != null ? new Member(user, null!, new List<Snowflake>()) : null;
        }
    }
}