using System.Collections.Generic;
using System.Threading.Tasks;
using Lavalink4NET.Player;

namespace Bot.Utilities.Music {
    public interface IMusicProvider {
        Task<bool> CanProvide();
        Task<List<LavalinkTrack>> Provide();
    }
}