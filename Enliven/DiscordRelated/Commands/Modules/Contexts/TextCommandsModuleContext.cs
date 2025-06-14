using System.Threading.Tasks;
using Common;
using Discord;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands.Modules.Contexts;

public class TextCommandsModuleContext(ICommandContext originalContext) : ICommonModuleContext, ICommandContext {
    public ICommandContext OriginalContext { get; } = originalContext;
    public IUserMessage Message => OriginalContext.Message;

    public IDiscordClient Client => OriginalContext.Client;
    public IGuild Guild => OriginalContext.Guild;
    public IMessageChannel Channel => OriginalContext.Channel;
    public IUser User => OriginalContext.User;
    public bool NeedResponse => false;
    public bool CanSendEphemeral => false;

    public ValueTask BeforeExecuteAsync() {
        //-V3013
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterExecuteAsync() {
        return ValueTask.CompletedTask;
    }

    public Task<SentMessage> SendMessageAsync(string? text, Embed[]? embeds, bool ephemeral = false, MessageComponent? components = null) {
        return Channel.SendMessageAsync(text, embeds: embeds, components: components)
            .PipeAsync(message => new SentMessage(message, false));
    }
}