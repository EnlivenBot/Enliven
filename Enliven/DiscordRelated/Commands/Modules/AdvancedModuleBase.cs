using System;
using System.Threading.Tasks;
using Autofac;
using Bot.DiscordRelated.Commands.Modules.Contexts;
using Bot.DiscordRelated.Interactions.Wrappers;
using Common.Config;
using Common.Localization.Providers;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Interactions.Builders;
using ModuleInfo = Discord.Interactions.ModuleInfo;

namespace Bot.DiscordRelated.Commands.Modules;

public class AdvancedModuleBase : IModuleBase, IInteractionModuleBase, IGenericModuleBase {
    private GuildConfig? _guildConfig;
    private ILocalizationProvider? _loc;
    public IComponentContext ComponentContext { get; set; } = null!;
    [DontInject] public ILocalizationProvider Loc => _loc ??= GuildConfig.Loc;
    [DontInject] public GuildConfig GuildConfig => _guildConfig ??= ComponentContext.Resolve<IGuildConfigProvider>().Get(Context.Guild.Id);

    public ICommonModuleContext Context { get; private set; } = null!;

    #region Interactions
    public void SetContext(IInteractionContext context) {
        if (context is not EnlivenInteractionContextWrapper enlivenInteractionContextWrapper) {
            throw new ArgumentException("Invalid interaction context type");
        }
        Context = new InteractionsModuleContext(enlivenInteractionContextWrapper);
    }
    /// <inheritdoc />
    public virtual async Task BeforeExecuteAsync(ICommandInfo command) {
        await Context.BeforeExecuteAsync();
    }
    /// <inheritdoc />
    public void BeforeExecute(ICommandInfo command) { }
    /// <inheritdoc />
    public virtual async Task AfterExecuteAsync(ICommandInfo command) {
        await Context.AfterExecuteAsync();
    }
    /// <inheritdoc />
    public void AfterExecute(ICommandInfo command) { }
    /// <inheritdoc />
    public void OnModuleBuilding(InteractionService commandService, ModuleInfo module) { }
    /// <inheritdoc />
    public void Construct(ModuleBuilder builder, InteractionService commandService) { }

    #endregion

    #region Text

    /// <inheritdoc />
    public void SetContext(ICommandContext context) {
        Context = new TextCommandsModuleContext(context);
    }
    public virtual async Task BeforeExecuteAsync(CommandInfo command) {
        await Context.BeforeExecuteAsync();
    }
    /// <inheritdoc />
    public void BeforeExecute(CommandInfo command) { }
    public virtual async Task AfterExecuteAsync(CommandInfo command) {
        await Context.BeforeExecuteAsync();
    }
    /// <inheritdoc />
    public void AfterExecute(CommandInfo command) { }
    /// <inheritdoc />
    public void OnModuleBuilding(CommandService commandService, Discord.Commands.Builders.ModuleBuilder builder) { }

    #endregion
}