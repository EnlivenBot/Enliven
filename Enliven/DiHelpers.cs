using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.Interactions.Handlers;
using Bot.DiscordRelated.Music;
using Bot.Utilities.Collector;
using Common;
using Microsoft.Extensions.DependencyInjection;

namespace Bot;

internal static class DiHelpers {
    public static IServiceCollection AddPerBotServices(this IServiceCollection services) {
        // Providers
        services.AddSingleton<EmbedPlayerDisplayProvider>();
        services.AddSingleton<IService>(s => s.GetRequiredService<EmbedPlayerDisplayProvider>());
        services.AddSingleton<EmbedPlayerQueueDisplayProvider>();
        services.AddSingleton<EmbedPlayerEffectsDisplayProvider>();

        // Services
        services.AddSingleton<CustomCommandService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<CustomCommandService>());
        services.AddSingleton<CustomInteractionService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<CustomInteractionService>());
        services.AddSingleton<GlobalBehaviorsService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<GlobalBehaviorsService>());
        services.AddSingleton<ScopedReliabilityService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<ScopedReliabilityService>());
        services.AddSingleton<InteractionsHandlerService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<InteractionsHandlerService>());
        services.AddSingleton<CommandHandlerService>();
        services.AddSingleton<IService>(s => s.GetRequiredService<CommandHandlerService>());
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<MessageComponentInteractionsHandler>();
        services.AddSingleton<CollectorService>();

        services.AddSingleton<IInteractionsHandler, DiscordNetInteractionsHandler>();
        services.AddSingleton<IInteractionsHandler, EmbedPlayerDisplayRestoreInteractionsHandler>();
        services.AddSingleton<IInteractionsHandler>(s => s.GetRequiredService<MessageComponentInteractionsHandler>());

        return services;
    }
}