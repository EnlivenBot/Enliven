using System;

namespace Bot.DiscordRelated.UpdatableMessage;

public interface IUpdatableMessageDisplayBehavior : IDisposable {
    void OnAttached(UpdatableMessageDisplay display);
}