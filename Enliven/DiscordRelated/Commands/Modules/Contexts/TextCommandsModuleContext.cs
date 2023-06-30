using System.Threading.Tasks;
using Common;
using Discord;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands.Modules.Contexts;

public class TextCommandsModuleContext : ICommonModuleContext, ICommandContext {
    public TextCommandsModuleContext(ICommandContext originalContext) {
        OriginalContext = originalContext;
    }
    public ICommandContext OriginalContext { get; }
    public IUserMessage Message => OriginalContext.Message;

    public IDiscordClient Client => OriginalContext.Client;
    public IGuild Guild => OriginalContext.Guild;
    public IMessageChannel Channel => OriginalContext.Channel;
    public IUser User => OriginalContext.User;
    public bool NeedResponse => false;
    public bool HasMeaningResponseSent { get; private set; }
    public bool CanSendEphemeral => false;

    public ValueTask BeforeExecuteAsync() {
        //-V3013
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterExecuteAsync() {
        return ValueTask.CompletedTask;
    }

    public Task<SentMessage> SendMessageAsync(string? text, Embed[]? embeds, bool ephemeral = false, MessageComponent? components = null) {
        HasMeaningResponseSent = true;
        return Channel.SendMessageAsync(text, embeds: embeds, components: components)
            .PipeAsync(message => new SentMessage(message, false));
    }
}