using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Commands.Modules.Contexts;

public interface ICommonModuleContext {
    /// <summary>
    ///     Gets the <see cref="IDiscordClient" /> that the command is executed with.
    /// </summary>
    IDiscordClient Client { get; }

    /// <summary>
    ///     Gets the <see cref="IGuild" /> that the command is executed in.
    /// </summary>
    IGuild Guild { get; }

    /// <summary>
    ///     Gets the <see cref="IMessageChannel" /> that the command is executed in.
    /// </summary>
    IMessageChannel Channel { get; }

    /// <summary>
    ///     Gets the <see cref="IUser" /> who executed the command.
    /// </summary>
    IUser User { get; }

    bool NeedResponse { get; }
    bool HasMeaningResponseSent { get; }
    bool CanSendEphemeral { get; }

    public ValueTask BeforeExecuteAsync();
    public ValueTask AfterExecuteAsync();
    Task<SentMessage> SendMessageAsync(string? text, Embed[]? embeds, bool ephemeral = false, MessageComponent? components = null);
}