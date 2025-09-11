using System.Threading.Tasks;
using Bot.Music.Players;

namespace Bot.DiscordRelated.Music;

#pragma warning disable 4014
public interface IEmbedPlayerDisplayBackgroundUpdatable : IPlayerDisplay {
    Task Update();
    bool UpdateViaInteractions { get; }
}