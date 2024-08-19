using System.Threading.Tasks;

namespace Common.Music.Players;

public interface IPlayerOnReady
{
    Task OnReady();
}