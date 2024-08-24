using System.Threading.Tasks;

namespace Common.Music.Tracks;

public interface ITrackNeedPrefetch
{
    /// <summary>
    /// Called one track before playing this one
    /// </summary>
    public Task PrefetchTrack();
}