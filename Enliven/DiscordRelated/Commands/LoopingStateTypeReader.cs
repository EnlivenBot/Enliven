using System;
using System.Threading.Tasks;
using Common.Music.Players;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands {
    public class LoopingStateTypeReader : CustomTypeReader {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            return Task.FromResult(Enum.TryParse(input, out LoopingState result)
                ? TypeReaderResult.FromSuccess(result)
                : TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a looping state."));
        }
        
        public override Type GetTargetType() {
            return typeof(LoopingState);
        }
    }
}