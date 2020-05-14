using System;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Music;
using Bot.Music.Players;
using Discord.Commands;

namespace Bot.Commands {
    public class ChannelFunctionTypeReader : TypeReader {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            return Task.FromResult(Enum.TryParse(input, out ChannelFunction result)
                ? TypeReaderResult.FromSuccess(result)
                : TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a channel function."));
        }
    }

    public class LoopingStateTypeReader : TypeReader {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            return Task.FromResult(Enum.TryParse(input, out LoopingState result)
                ? TypeReaderResult.FromSuccess(result)
                : TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a looping state."));
        }
    }
    
    public class BassBoostModeTypeReader : TypeReader {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            return Task.FromResult(Enum.TryParse(input, out BassBoostMode result)
                ? TypeReaderResult.FromSuccess(result)
                : TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a bass boost mode."));
        }
    }
}