using System;
using System.Threading.Tasks;
using Bot.Utilities;
using Discord.Commands;

namespace Bot.Commands
{
    public class ChannelFunctionTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            if (ChannelUtils.ChannelFunction.TryParse(input, out ChannelUtils.ChannelFunction result))
                return Task.FromResult(TypeReaderResult.FromSuccess(result));

            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a Channel Function."));
        }
    }
}
