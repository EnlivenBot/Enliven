using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;

namespace Common.Utils;

public static class DiscordHelper
{
    public delegate bool NeedFetchSize(string format);

    private static readonly Regex AttachmentParseRegex = new(@"\/(\d+)\/([^\/]+\.\S+)");
    public static NeedFetchSize AlwaysFetch = _ => true;
    public static NeedFetchSize NeverFetch = _ => false;

    public static async Task<IAttachment> ParseAttachmentFromUrlAsync(string url, NeedFetchSize needFetchSizePredicate)
    {
        var match = AttachmentParseRegex.Match(url);
        var id = ulong.Parse(match.Groups[1].Value);
        var name = match.Groups[2].Value;
        var format = Path.GetExtension(name);

        var size = 0L;
        if (needFetchSizePredicate?.Invoke(format) ?? false)
        {
            size = await WebUtilities.GetFileSizeFromUrlAsync(url) ?? 0;
        }

        return new FakeAttachment(id, name, url, url, (int)size, 0, 0);
    }

    private class FakeAttachment : IAttachment
    {
        public FakeAttachment(ulong id, string filename, string url, string proxyUrl, int size, int? height, int? width)
        {
            Id = id;
            Filename = filename;
            Url = url;
            ProxyUrl = proxyUrl;
            Size = size;
            Height = height;
            Width = width;
        }

        public ulong Id { get; }
        public string Filename { get; }
        public string Url { get; }
        public string ProxyUrl { get; }
        public int Size { get; }
        public int? Height { get; }
        public int? Width { get; }
        public bool Ephemeral { get; }
        public string Description { get; }
        public string ContentType { get; }

        /// <inheritdoc />
        public double? Duration { get; }

        /// <inheritdoc />
        public string Waveform { get; }

        /// <inheritdoc />
        public AttachmentFlags Flags { get; }

        public IReadOnlyCollection<IUser> ClipParticipants { get; }
        public string Title { get; }
        public DateTimeOffset? ClipCreatedAt { get; }

        public DateTimeOffset CreatedAt { get; }
    }
}