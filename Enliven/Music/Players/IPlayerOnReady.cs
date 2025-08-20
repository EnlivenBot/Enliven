using System.Threading.Tasks;

namespace Bot.Music.Players;

public interface IPlayerOnReady {
    Task OnReady();
}