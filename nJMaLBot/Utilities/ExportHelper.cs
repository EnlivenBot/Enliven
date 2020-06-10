using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiffMatchPatch;
using Discord;
using Discord.WebSocket;
using DiscordChatExporter.Core.Models;
using DiscordChatExporter.Core.Rendering;
using Attachment = DiscordChatExporter.Core.Models.Attachment;
using ChannelType = DiscordChatExporter.Core.Models.ChannelType;
using Embed = DiscordChatExporter.Core.Models.Embed;
using MessageType = DiscordChatExporter.Core.Models.MessageType;

namespace Bot.Utilities {
    public class ExportHelper {
        private static IMessageRenderer CreateRenderer(string outputPath, ExportFormat format, RenderContext context) {
            // Create output directory
            var dirPath = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dirPath))
                Directory.CreateDirectory(dirPath);

            // Create renderer

            switch (format) {
                case ExportFormat.PlainText:
                    return new PlainTextMessageRenderer(outputPath, context);
                case ExportFormat.Csv:
                    return new CsvMessageRenderer(outputPath, context);
                case ExportFormat.HtmlDark:
                    return new HtmlMessageRenderer(outputPath, context, "Dark", false, true);
                case ExportFormat.HtmlLight:
                    return new HtmlMessageRenderer(outputPath, context, "Light");
                default:
                    throw new InvalidOperationException($"Unknown export format [{format}].");
            }
        }

        public static async Task ExportHistoryAsync(MessageHistory messageHistory, string outputPath) {
            // Create context
            var diffMatchPatch = new DiffMatchPatch.DiffMatchPatch();
            var guild = (SocketGuild) ((IGuildChannel) Program.Client.GetChannel(messageHistory.ChannelId)).Guild;
            var mentionableUsers = new HashSet<User>(IdBasedEqualityComparer.Instance);
            var mentionableChannels = guild.TextChannels;
            var mentionableRoles = guild.Roles;

            var context = new RenderContext
            (
                new Guild("", "", ""), new Channel("", "", "", "", "", ChannelType.GuildTextChat), null, null, "dd-MMM-yy hh:mm tt",
                mentionableUsers,
                new ReadOnlyCollection<Channel>(mentionableChannels.Select(discordChannel => new Channel(discordChannel.Id.ToString(), null,
                    guild.Id.ToString(), discordChannel.Name, discordChannel.Topic,
                    ChannelType.GuildTextChat)).ToList()),
                new ReadOnlyCollection<Role>(mentionableRoles.Select(discordRole => new Role(discordRole.Id.ToString(), discordRole.Name)).ToList())
            );

            var path = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Render messages
            var renderer = CreateRenderer(path, ExportFormat.HtmlDark, context);

            var lastString = "";
            foreach (var snapshotPatches in messageHistory.Edits.Select(snapshot =>
                new Tuple<List<Patch>, MessageHistory.MessageSnapshot>(
                    DiffMatchPatch.DiffMatchPatch.patch_fromText(snapshot.Value), snapshot))) {
                var currentString = diffMatchPatch.patch_apply(snapshotPatches.Item1, lastString)[0].ToString();
                // var diffs = diffMatchPatch.diff_main(lastString, currentString);
                // diffMatchPatch.diff_cleanupSemantic(diffs);
                lastString = currentString;
                // Add encountered users to the list of mentionable users
                mentionableUsers.Add(ConstructUser(messageHistory.AuthorId));
                foreach (var userMention in GetUserMentions(lastString)) mentionableUsers.Add(userMention);

                // var htmlText = diffMatchPatch.diff_prettyHtml(diffs);
                // var replacedHtml = htmlText.Replace("<span>", "�").Replace("</span>", "�")
                //                            .Replace("<del style=\"background:#ffe6e6;\">", "�").Replace("</del>", "�")
                //                            .Replace("<ins style=\"background:#e6ffe6;\">", "�").Replace("</ins>", "�");
                // Render message
                await renderer.RenderMessageAsync(new Message(
                    messageHistory.Id, messageHistory.ChannelId.ToString(), MessageType.Default, ConstructUser(messageHistory.AuthorId),
                    messageHistory.Edits.First().EditTimestamp, snapshotPatches.Item2.EditTimestamp, false, lastString, new List<Attachment>(),
                    new List<Embed>(), new List<Reaction>(), new List<User>()));
            }

            // Flush last renderer
            await renderer.DisposeAsync();
            // var text = File.ReadAllText(outputPath);
            // text = text.Replace("�", "<span>").Replace("�", "</span>")
            //            .Replace("�", "<span style=\"background:#710505;\">").Replace("�", "</span>")
            //            .Replace("�", "<span style=\"background:#0A450D;\">").Replace("�", "</span>");
            // File.WriteAllText(outputPath, text);
        }

        private static User ConstructUser(ulong userId) {
            return ConstructUser(Program.Client.GetUser(userId));
        }

        private static User ConstructUser(SocketUser user) {
            return new User(user.Id.ToString(), user.DiscriminatorValue, user.Username, user.AvatarId, user.IsBot);
        }

        private static IEnumerable<User> GetUserMentions(string text) {
            foreach (Match m in Regex.Matches(text, @"(?<=<@|<@!)[0-9]{18}(?=>)", RegexOptions.Multiline))
                yield return ConstructUser(Convert.ToUInt64(m.Value));
        }
    }
}