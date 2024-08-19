using System.Threading.Tasks;

namespace Common.Music.Players;

public interface IPlayerShutdownInternally
{
    Task<PlayerSnapshot> ShutdownInternal();
}