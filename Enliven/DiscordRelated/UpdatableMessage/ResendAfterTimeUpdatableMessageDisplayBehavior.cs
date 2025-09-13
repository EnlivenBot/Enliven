using System;
using System.Threading.Tasks;

namespace Bot.DiscordRelated.UpdatableMessage;

public class ResendAfterTimeUpdatableMessageDisplayBehavior(TimeSpan resendAfter)
    : IUpdatableMessageDisplayResendBehavior {
    private IDisposable? _disposable;
    private DateTime? _lastMessageSendTime;

    public void OnAttached(UpdatableMessageDisplay display) {
        _disposable = display.MessageChanged
            .DistinctUntilNewMessage()
            .Subscribe(OnDisplayMessageChanged);
    }

    private void OnDisplayMessageChanged(ulong messageId) {
        _lastMessageSendTime = DateTime.Now;
    }

    public ValueTask<bool> ShouldResend() {
        return ValueTask.FromResult(
            _lastMessageSendTime.HasValue &&
            DateTime.Now - _lastMessageSendTime.Value > resendAfter);
    }

    public void Dispose() {
        _disposable?.Dispose();
    }
}