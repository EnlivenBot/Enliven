using System;
using System.Threading.Tasks;

namespace Common.Music.Tracks {
    public interface ITrackHasArtwork {
        public ValueTask<Uri?> GetArtwork();
    }
}