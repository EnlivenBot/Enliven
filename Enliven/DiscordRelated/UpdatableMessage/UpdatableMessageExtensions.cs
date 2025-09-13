using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Common.Utils;

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
}