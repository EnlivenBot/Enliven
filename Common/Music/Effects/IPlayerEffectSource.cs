using System.Threading.Tasks;
using Common.Config;

namespace Common.Music.Effects;

public interface IPlayerEffectSource
{
    Task<PlayerEffect> CreateEffect(string? args);
    string GetSourceName();
}