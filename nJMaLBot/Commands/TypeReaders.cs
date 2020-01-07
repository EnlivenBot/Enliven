using System;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities;
using Discord.Commands;

namespace Bot.Commands
{
    public class ChannelFunctionTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            return Task.FromResult(Enum.TryParse(input, out ChannelFunction result) ? TypeReaderResult.FromSuccess(result) : TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a Channel Function."));
        }
    }
}
