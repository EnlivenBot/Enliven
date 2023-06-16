using System;
using System.Threading.Tasks;
using Common.Config;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands {
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
}