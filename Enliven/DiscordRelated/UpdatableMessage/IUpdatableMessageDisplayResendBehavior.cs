using System.Threading.Tasks;

namespace Bot.DiscordRelated.UpdatableMessage;

public interface IUpdatableMessageDisplayResendBehavior : IUpdatableMessageDisplayBehavior {
    ValueTask<bool> ShouldResend();
}