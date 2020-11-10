using System;
using System.Threading.Tasks;
using Common.Config;
using Common.Music;
using Common.Music.Players;
using Discord.Commands;
using Org.BouncyCastle.Asn1.Cmp;

namespace Bot.DiscordRelated.Commands {
    public abstract class CustomTypeReader : TypeReader {
        public abstract override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services);
        public abstract Type GetTargetType();
    }
    
    public class ChannelFunctionTypeReader : CustomTypeReader {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            return Task.FromResult(Enum.TryParse(input, out ChannelFunction result)
                ? TypeReaderResult.FromSuccess(result)
                : TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a channel function."));
        }

        public override Type GetTargetType() {
            return typeof(ChannelFunction);
        }
    }

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
    
    public class BassBoostModeTypeReader : CustomTypeReader {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            return Task.FromResult(Enum.TryParse(input, out BassBoostMode result)
                ? TypeReaderResult.FromSuccess(result)
                : TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a bass boost mode."));
        }
        
        public override Type GetTargetType() {
            return typeof(BassBoostMode);
        }
    }
}