using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Attributes;
using Discord;
using Discord.Interactions;

namespace Bot.DiscordRelated.Commands.Modules.Contexts {
    public class InteractionsModuleContext : ICommonModuleContext, IInteractionContext {
        public IInteractionContext OriginalContext { get; }
        private Func<ICommandInfo> _commandResolver;
        public InteractionsModuleContext(IInteractionContext originalContext, Func<ICommandInfo> commandResolver) {
            _commandResolver = commandResolver;
            OriginalContext = originalContext;
        }

        public IDiscordClient Client => OriginalContext.Client;
        public IGuild Guild => OriginalContext.Guild;
        public IMessageChannel Channel => OriginalContext.Channel;
        public IUser User => OriginalContext.User;
        public IDiscordInteraction Interaction => OriginalContext.Interaction;

        public bool HasLoadingSent { get; private set; }

        public async ValueTask BeforeExecuteAsync() {
            if (!NeedLoadingSend(_commandResolver())) return;
            HasLoadingSent = true;
            await Interaction.DeferAsync();
        }

        public async ValueTask AfterExecuteAsync() {
            var restInteractionMessage = await Interaction.GetOriginalResponseAsync();
            if ((restInteractionMessage.Flags & MessageFlags.Loading) != 0) await restInteractionMessage.DeleteAsync();
        }

        private static bool NeedLoadingSend(ICommandInfo commandInfo) {
            var longRunningAttribute = commandInfo.Attributes.OfType<LongRunningCommandAttribute>().FirstOrDefault()
                                    ?? commandInfo.Module.Attributes.OfType<LongRunningCommandAttribute>().FirstOrDefault();
            return longRunningAttribute?.IsLongRunning ?? false;
        }

        public async Task<SentMessage> SendMessageAsync(string? text, Embed[]? embeds, bool ephemeral = false, MessageComponent? components = null) {
            // No loading and no responded
            if (!HasLoadingSent && !Interaction.HasResponded) {
                await Interaction.RespondAsync(text: text, embeds: embeds, ephemeral: ephemeral, components: components);
                return new SentMessage(() => Interaction.GetOriginalResponseAsync(), ephemeral);
            }
            // Only loading sent
            if (HasLoadingSent && !Interaction.HasResponded) {
                var message0 = await Interaction.ModifyOriginalResponseAsync(properties => {
                    properties.Content = text ?? Optional<string>.Unspecified;
                    properties.Components = components ?? Optional<MessageComponent>.Unspecified;
                    properties.Embeds = embeds ?? Optional<Embed[]>.Unspecified;
                });
                return new SentMessage(message0, false);
            }
            var message1 = await Interaction.FollowupAsync(text: text, embeds: embeds, ephemeral: ephemeral, components: components);
            return new SentMessage(message1, ephemeral);
        }
    }
}