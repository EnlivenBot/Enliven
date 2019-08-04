using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Discord;
using Discord.WebSocket;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Newtonsoft.Json;

namespace SKProCH_s_Discord_Bot
{
    public class MessageStorage
    {
        public ulong  AuthorId     { get; set; }
        public string AuthorName   { get; set; }
        public string AuthorAvatar { get; set; }

        public ulong Id        { get; set; }
        public ulong ChannelId { get; set; }
        public ulong GuildId   { get; set; }

        public DateTimeOffset        CreationDate  { get; set; }
        public string                UrlToNavigate { get; set; }
        public List<MessageSnapshot> Edits         { get; set; } = new List<MessageSnapshot>();

        public class MessageSnapshot
        {
            public DateTimeOffset? EditTimestamp { get; set; }
            public string          Content       { get; set; }
        }

        public SocketGuildUser GetAuthor() { return Program.Client.GetGuild(GuildId).GetUser(this.AuthorId); }

        public string GetAuthorName() { return GetAuthor() != null ? GetAuthor().Nickname != null ? $"{GetAuthor().Nickname} ({GetAuthor().Username})" : GetAuthor().Username : this.AuthorName; }

        public string GetAuthorIcon() { return GetAuthor() != null ? GetAuthor().GetAvatarUrl() : this.AuthorAvatar; }

        public List<EmbedFieldBuilder> BuildLog() {
            List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>();
            foreach (var messageSnapshot in this.Edits) {
                fields.Add(new EmbedFieldBuilder().WithIsInline(false)
                                                  .WithName(messageSnapshot.EditTimestamp.Value.ToOffset(TimeSpan.FromHours(3)).DateTime.ToString())
                                                  .WithValue($">>> {messageSnapshot.Content}"));
            }

            return fields;
        }

        public Embed BuildEmbed() {
            EmbedBuilder eb = new EmbedBuilder();
            return eb.WithTitle($"Просмотр истории изменений")
                     .WithDescription($"[этого]({this.UrlToNavigate}) сообщения")
                     .WithAuthor(GetAuthorName(), GetAuthorIcon())
                     .WithFields(BuildLog())
                     .WithColor(Color.Gold)
                     .Build();
        }


        #region IO Mehtods

        public void Save() {
            var dir = Path.Combine("messageEditsLogs", GuildId.ToString(), ChannelId.ToString());
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir,    Id + ".json");
            var zippath = Path.Combine(dir, Id + ".zip");

            File.WriteAllText(path, JsonConvert.SerializeObject(this));
            File.Delete(zippath);
            using (ZipOutputStream outputStream = new ZipOutputStream(File.Create(zippath))) {
                byte[] buffer = new byte[4096];
                outputStream.SetLevel(9);
                ZipEntry entry = new ZipEntry(Path.GetFileName(path));
                outputStream.PutNextEntry(entry);
                using (FileStream fs = File.OpenRead(path)) {
                    int sourceBytes;
                    do {
                        sourceBytes = fs.Read(buffer, 0, buffer.Length);
                        outputStream.Write(buffer, 0, sourceBytes);
                    } while (sourceBytes > 0);
                }
                outputStream.Finish();
                outputStream.Close();
            }
            File.Delete(path);
        }

        public void DeleteMessage() {
            var dir = Path.Combine("messageEditsLogs", GuildId.ToString(), ChannelId.ToString());
            var path = Path.Combine(dir,               Id + ".json");
            var zippath = Path.Combine(dir,            Id + ".zip");
            File.Delete(zippath);
        }

        public void DeleteChannel() {
            var dir = Path.Combine("messageEditsLogs", GuildId.ToString(), ChannelId.ToString());
            FileUtils.RecursiveFoldersDelete(dir);
        }

        public void DeleteGuild() {
            var dir = Path.Combine("messageEditsLogs", GuildId.ToString());
            FileUtils.RecursiveFoldersDelete(dir);
        }

        #endregion

        #region Static Methods

        public static MessageStorage Load(ulong guildId, ulong channelId, string messageId) {
            if (messageId.Contains('-'))
                messageId = messageId.Split('-')[1];
            return Load(guildId, channelId, Convert.ToUInt64(messageId));
        }

        public static MessageStorage Load(ulong guildId, ulong channelId, ulong messageId) {
            var dir = Path.Combine("messageEditsLogs", guildId.ToString(), channelId.ToString());
            var path = Path.Combine(dir,               messageId + ".json");
            var zippath = Path.Combine(dir,            messageId + ".zip");

            if (!File.Exists(zippath))
                return null;

            ZipFile file = null;
            try {
                FileStream fs = File.OpenRead(zippath);
                file = new ZipFile(fs);

                foreach (ZipEntry zipEntry in file) {
                    if (!zipEntry.IsFile) {
                        // Ignore directories
                        continue;
                    }

                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    // 4K is optimum
                    byte[] buffer = new byte[4096];
                    Stream zipStream = file.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(dir, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);

                    if (directoryName.Length > 0) {
                        Directory.CreateDirectory(directoryName);
                    }

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath)) {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
            finally {
                if (file != null) {
                    file.IsStreamOwner = true; // Makes close also shut the underlying stream
                    file.Close();              // Ensure we release resources
                }
            }

            MessageStorage thisMessage = JsonConvert.DeserializeObject<MessageStorage>(File.ReadAllText(path));
            File.Delete(path);
            return thisMessage;
        }

        public static void Delete(ulong guildId, ulong channelId, ulong messageId) {
            var dir = Path.Combine("messageEditsLogs", guildId.ToString(), channelId.ToString());
            var path = Path.Combine(dir,               messageId + ".json");
            var zippath = Path.Combine(dir,            messageId + ".zip");
            File.Delete(zippath);
        }

        public static void Delete(ulong guildId, ulong channelId) {
            var dir = Path.Combine("messageEditsLogs", guildId.ToString(), channelId.ToString());
            FileUtils.RecursiveFoldersDelete(dir);
        }

        public static void Delete(ulong guildId) {
            var dir = Path.Combine("messageEditsLogs", guildId.ToString());
            FileUtils.RecursiveFoldersDelete(dir);
        }

        #endregion
    }
}
