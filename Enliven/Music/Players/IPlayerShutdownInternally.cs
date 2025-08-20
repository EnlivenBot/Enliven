using System.Threading.Tasks;
using Common.Music;

namespace Bot.Music.Players;

public interface IPlayerShutdownInternally {
    Task<PlayerSnapshot> ShutdownInternal();
}