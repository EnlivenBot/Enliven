using Bot.DiscordRelated.Commands;
using Discord;
using Discord.Commands;

namespace Bot.DiscordRelated.Interactions {
    public class InteractionFallbackCommandContext : ControllableCommandContext {
        public InteractionFallbackCommandContext(IInteractionContext context) 
            : base(context.Client) {
            Guild = context.Guild;
            Channel = context.Channel;
            User = context.User;
            Interaction = context.Interaction;
        }
        
        public IDiscordInteraction Interaction { get; }
    }
}