using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public interface IEnlivenInteraction : IDiscordInteraction
{
    bool NeedResponse { get; }
    bool CurrentResponseDeferred { get; }
    bool CurrentResponseMeaningful { get; }
}