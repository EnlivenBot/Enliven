using System.Threading.Tasks;
using Bot.Utilities;
using Discord;
using Discord.Commands;

namespace Bot.Commands
{
    public class GetLogByMessage : ModuleBase
    {
        [Command("changes")]
        [Summary(                                           "Prints ")]
        public async Task PrintChanges([Remainder] [Summary("Message ID to print")]
                                       string id) {
            await Context.Channel.SendMessageAsync("", false, MessageStorage.Load(Context.Guild.Id, Context.Channel.Id, id).BuildEmbed());
        }
    }

    public class ChannelsFunctions : ModuleBase
    {
        [Command("setchannel")]
        [Summary(                                         "Prints ")]
        public async Task SetChannel([Summary("Message ID to print")]
                                     ChannelUtils.ChannelFunction func, IChannel channel) {
            ChannelUtils.SetChannel(Context.Guild.Id, channel.Id, func);
            await Context.Message.DeleteAsync();
        }

        [Command("setchannel")]
        [Summary(                                             "Prints ")]
        public async Task SetThisChannel(ChannelUtils.ChannelFunction func) {
            ChannelUtils.SetChannel(Context.Guild.Id, Context.Channel.Id, func);
            await Context.Message.DeleteAsync();
        }
    }
}
