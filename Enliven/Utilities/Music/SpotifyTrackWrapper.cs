using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace Bot.Utilities.Music {
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

        public async Task<FullTrack> GetFullTrack() {
            return _track ??= await (await SpotifyMusicResolver.SpotifyClient)!.Tracks.Get(Id);
        }
        
        public async Task<string> GetTrackInfo() {
            if (_trackInfo != null)
                return _trackInfo;
            var fullTrack = await GetFullTrack();
            return _trackInfo = $"{fullTrack.Name} - {fullTrack.Artists[0].Name}";
        }
    }
}