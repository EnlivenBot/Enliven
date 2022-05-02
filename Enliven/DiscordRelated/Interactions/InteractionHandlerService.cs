using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Utils;
using Discord;
using Discord.Interactions;
using NLog;

namespace Bot.DiscordRelated.Interactions {
    public class InteractionHandlerService : IService, IDisposable {
        private IDisposable? _disposable;
        private readonly CustomInteractionService _customInteractionService;
        private readonly EnlivenShardedClient _enlivenShardedClient;
        private readonly ILogger _logger;
        private readonly ServiceProviderAdapter _serviceProvider;
        public InteractionHandlerService(IComponentContext serviceContainer, CustomInteractionService customInteractionService, EnlivenShardedClient enlivenShardedClient, ILogger logger) {
            _serviceProvider = new ServiceProviderAdapter(serviceContainer);
            _customInteractionService = customInteractionService;
            _enlivenShardedClient = enlivenShardedClient;
            _logger = logger;
        }

        public async Task OnDiscordReady() {
            await _customInteractionService.RegisterCommandsGloballyAsync();
            _disposable = _enlivenShardedClient.InteractionCreate
                .Select(interaction => new ShardedInteractionContext(_enlivenShardedClient, interaction))
                .SubscribeAsync(OnInteractionCreated);
            _logger.Info("Interactions initialized");
        }

        public void Dispose() {
            _disposable?.Dispose();
        }

        private async Task OnInteractionCreated(ShardedInteractionContext context) {
            try {
                var interactionSearchResult = SearchInteraction(context);
                if (!interactionSearchResult.IsSuccess) {
                    _logger.Warn("Interaction not found. Id: {InteractionId}. Reason: {Reason}", interactionSearchResult.Text, interactionSearchResult.ErrorReason);
                    return;
                }

                _ = context.Interaction.DeferAsync();

                var result = await interactionSearchResult.Command.ExecuteAsync(context, _serviceProvider).ConfigureAwait(false);
                if (!result.IsSuccess) {
                    var exception = result is ExecuteResult executeResult ? executeResult.Exception : null;
                    if (exception is not CommandInterruptionException)
                        _logger.Error(exception, "Interaction execution {Result}: {Reason}", result.Error!.Value, result.ErrorReason);
                }

                try {
                    var restInteractionMessage = await context.Interaction.GetOriginalResponseAsync();
                    if ((restInteractionMessage.Flags & MessageFlags.Loading) != 0) {
                        await restInteractionMessage.DeleteAsync();
                    }
                }
                catch (Exception) {
                    // ignored
                }
            }
            catch (Exception e) {
                _logger.Error(e, "Error while handling interaction");
            }
        }

        private SearchResult<ICommandInfo> SearchInteraction(ShardedInteractionContext context) {
            return context.Interaction switch {
                ISlashCommandInteraction slashCommandInteraction => ParseSearchResultToCommon(_customInteractionService.SearchSlashCommand(slashCommandInteraction)),
                IComponentInteraction messageComponent           => ParseSearchResultToCommon(_customInteractionService.SearchComponentCommand(messageComponent)),
                IUserCommandInteraction userCommand              => ParseSearchResultToCommon(_customInteractionService.SearchUserCommand(userCommand)),
                IMessageCommandInteraction messageCommand        => ParseSearchResultToCommon(_customInteractionService.SearchMessageCommand(messageCommand)),
                IAutocompleteInteraction autocomplete            => ParseSearchResultToCommon(_customInteractionService.SearchAutocompleteCommand(autocomplete)),
                _                                                => throw new ArgumentOutOfRangeException()
            };
        }

        private static SearchResult<ICommandInfo> ParseSearchResultToCommon<T>(SearchResult<T> result) where T : class, ICommandInfo {
            return result.IsSuccess
                ? SearchResult<ICommandInfo>.FromSuccess(result.Text, result.Command, result.RegexCaptureGroups)
                : SearchResult<ICommandInfo>.FromError(result.Text, result.Error!.Value, result.ErrorReason);
        }
    }
}