using Bot.DiscordRelated.Commands.Modules.Contexts;

namespace Bot.DiscordRelated.Commands.Modules;

public interface IGenericModuleBase {
    ICommonModuleContext Context { get; }
}