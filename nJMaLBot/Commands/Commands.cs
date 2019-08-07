using System;
using System.Threading.Tasks;
using Bot.Utilities;
using Discord;
using Discord.Commands;

namespace Bot.Commands
{
    public class GetLogByMessage : ModuleBase
    {
        [Command("history")]
        [Summary("Показывает историю изменений сообщения.\n" +
                 "Так-же можно поставить эмоцию 📖 под нужное сообщение")]
        public async Task PrintChanges([Remainder] [Summary("ID сообщения, чью историю нужно вывести. Так-же можно использовать пару `ID канала`-`ID сообщения`")]
                                       string id) {
            ulong channelId = Context.Channel.Id;
            string messageId = id;
            if (id.Contains('-')) {
                channelId = Convert.ToUInt64(id.Split('-')[0]);
                messageId = id.Split('-')[1];
            }

            var message = MessageStorage.Load(Context.Guild.Id, channelId, messageId);
            if (message != null)
                await Context.Channel.SendMessageAsync("", false, message.BuildEmbed());
        }
    }

    public class ChannelsFunctions : ModuleBase
    {
        [Command("setchannel")]
        [Summary(                             "Назначает канал, который будет использоваться ботом для определенного типа сообщений")]
        public async Task SetChannel([Summary("Название функции канала (`music`, `log`)")] ChannelUtils.ChannelFunction func, [Summary("Ссылка на канал")]IChannel channel) {
            ChannelUtils.SetChannel(Context.Guild.Id, channel.Id, func);
            await Context.Message.DeleteAsync();
        }

        [Command("setchannel")]
        [Summary("Назначает канал, который будет использоваться ботом для определенного типа сообщений")]
        public async Task SetThisChannel([Summary("Ссылка на канал")] ChannelUtils.ChannelFunction func) {
            ChannelUtils.SetChannel(Context.Guild.Id, Context.Channel.Id, func);
            await Context.Message.DeleteAsync();
        }
    }
}
