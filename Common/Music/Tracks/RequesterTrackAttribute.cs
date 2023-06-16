using Lavalink4NET.Player;

namespace Common.Music.Tracks {
    public class RequesterTrackAttribute : ILavalinkTrackAttribute {
        public RequesterTrackAttribute() { }

        public RequesterTrackAttribute(string? requester) {
            Requester = requester;
        }

        public string? Requester { get; set; }

        public virtual string GetRequester() {
            return Requester ?? "Unknown";
        }
    }

    public static class RequesterTrackAttributeExtensions {
        public static LavalinkTrack AddAuthor(this LavalinkTrack track, string author, bool replace = true) {
            track.AddAttribute(new RequesterTrackAttribute(author), replace);
            return track;
        }

        public static string GetRequester(this LavalinkTrack track) {
            if (track.TryGetAttribute(out RequesterTrackAttribute attribute)) {
                return attribute.GetRequester();
            }

            return "Unknown";
        }
    }
}