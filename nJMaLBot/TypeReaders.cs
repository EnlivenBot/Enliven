using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace SKProCH_s_Discord_Bot
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
