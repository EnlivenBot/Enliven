using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace Bot.Music.Spotify {
    public class SpotifyTrackWrapper {
        private FullTrack? _track;
        private string? _trackInfo;

        public SpotifyTrackWrapper(string id, FullTrack? track = null) {
            _track = track;
            Id = id;
        }
        
        public SpotifyTrackWrapper(SimpleTrack track) {
            _trackInfo = $"{track.Name} - {track.Artists[0].Name}";
            Id = track.Id;
        }

        public string Id { get; private set; }

        public async Task<FullTrack> GetFullTrack(SpotifyClient client) {
            return _track ??= await client.Tracks.Get(Id);
        }
        
        public async Task<string> GetTrackInfo(SpotifyClient client) {
            if (_trackInfo != null)
                return _trackInfo;
            var fullTrack = await GetFullTrack(client);
            return _trackInfo = $"{fullTrack.Name} - {fullTrack.Artists[0].Name}";
        }
    }
}