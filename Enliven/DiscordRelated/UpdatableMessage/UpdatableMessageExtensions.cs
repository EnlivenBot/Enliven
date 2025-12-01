using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Discord;
using Discord.Net;

namespace Bot.DiscordRelated.UpdatableMessage;

public static class UpdatableMessageExtensions {
    public static IObservable<ulong> DistinctUntilNewMessage(this IObservable<InteractionMessageHolder> source) {
        return source
            .Select(holder => Observable.FromAsync(async () => await holder.GetMessageIdAsync()))
            .Concat()
            .DistinctUntilChanged();
    }

    public static Task ExecuteForcedIfHasArgument<T, TForcedArg>(
        this SingleTask<T, TForcedArg> task, TForcedArg? forcedArg) {
        return forcedArg is not null ? task.ForcedExecute(forcedArg) : task.Execute();
    }

    public static async Task ModifyOrResendAsync(
        this InteractionMessageHolder? messageHolder,
        IMessageChannel fallbackTargetChannel,
        Action<MessageProperties> messagePropertiesUpdateCallback,
        RequestOptions? requestOptions = null) {
        if (messageHolder is not null) {
            try {
                await messageHolder.ModifyAsync(messagePropertiesUpdateCallback, requestOptions);
                return;
            }
            catch (HttpException e) when (e.DiscordCode == DiscordErrorCode.UnknownMessage) {
                // ignored
                return;
            }
        }

        var messageProperties = new MessageProperties();
        messagePropertiesUpdateCallback(messageProperties);
        await fallbackTargetChannel.SendMessageAsync(text: messageProperties.Content.GetValueOrDefault(),
            embed: messageProperties.Embed.GetValueOrDefault(),
            embeds: messageProperties.Embeds.GetValueOrDefault(),
            components: messageProperties.Components.GetValueOrDefault(),
            options: requestOptions);
    }
}