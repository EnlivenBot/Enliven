using System.Collections.Generic;
using System.Threading.Tasks;
using Common;
using Discord;
using Discord.Commands;

namespace Bot.Utilities.Collector {
    public class CommandCollectorEventArgs : CollectorEventArgsBase {
        public bool Handled { get; set; }
        
        public IMessage Message { get; private set; }
        public CommandMatch CommandInfo { get; private set; }
        public ParseResult ParseResult { get; private set; }
        public ICommandContext Context { get; private set; }

        public CommandCollectorEventArgs(CollectorController controller, IMessage message, KeyValuePair<CommandMatch, ParseResult> info,
                                         ICommandContext context) : base(controller) {
            Message = message;
            CommandInfo = info.Key;
            Context = context;
            ParseResult = info.Value;
        }

        public override Task RemoveReason() {
            Message.SafeDelete();
            return Task.CompletedTask;
        }

        // public async Task<IResult> ExecuteCommand(ICommandContext? overrideContext = null) {
        //     return await CommandInfo.ExecuteAsync(overrideContext ?? Context, ParseResult, EmptyServiceProvider.Instance).ConfigureAwait(false);
        // }
    }
}