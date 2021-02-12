using System;
using System.Threading.Tasks;
using Common.Music;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands {
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