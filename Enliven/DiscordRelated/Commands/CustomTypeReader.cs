using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands {
    public abstract class CustomTypeReader : TypeReader {
        public abstract override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services);
        public abstract Type GetTargetType();
    }
}