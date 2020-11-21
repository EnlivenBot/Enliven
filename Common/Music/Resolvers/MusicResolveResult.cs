using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lavalink4NET.Player;

namespace Common.Music.Resolvers {
    public class MusicResolveResult {
        public MusicResolveResult(Func<Task<bool>> canResolve, Func<Task<List<LavalinkTrack>>> resolve) {
            CanResolve = canResolve;
            Resolve = resolve;
        }

        public Func<Task<bool>> CanResolve { get; set; }
        public Func<Task<List<LavalinkTrack>>> Resolve { get; set; }
    }
}