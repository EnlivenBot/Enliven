using System;
using System.Threading.Tasks;
using Bot.Utilities;
using Discord;
using Discord.Commands;

namespace Bot.Commands {
    public class GetLogByMessage : ModuleBase {
        [Command("history")]
        [Summary("Показывает историю изменений сообщения.\n" +
                 "Так-же можно поставить эмоцию 📖 под нужное сообщение")]
        public async Task PrintChanges(
            [Remainder] [Summary("ID сообщения, чью историю нужно вывести. Так-же можно использовать пару `ID канала`-`ID сообщения`")]
            string id) {
            ulong channelId = Context.Channel.Id;
            string messageId = id;
            if (id.Contains('-')) {
                channelId = Convert.ToUInt64(id.Split('-')[0]);
                messageId = id.Split('-')[1];
            }

            var message = MessageStorage.Load(Context.Guild.Id, channelId, messageId);
            if (message != null)
                await Context.Channel.SendMessageAsync("", false, message.BuildEmbed(Localization.GetLanguage(Context.Guild.Id, Context.Channel.Id)));
        }
    }

    public class ChannelsFunctions : ModuleBase
    {
        [Command("setchannel")]
        [Summary("Назначает канал, который будет использоваться ботом для определенного типа сообщений")]
        public async Task SetChannel([Summary("Название функции канала (`music`, `log`)")]
                                     ChannelFunction func, [Summary("Ссылка на канал")] IChannel channel) {
            GuildConfig.Get(Context.Guild.Id).SetChannel(channel.Id.ToString(), func).Save();
            await Context.Message.DeleteAsync();
        }

        [Command("setchannel")]
        [Summary("Назначает канал, который будет использоваться ботом для определенного типа сообщений")]
        public async Task SetThisChannel([Summary("Ссылка на канал")] ChannelFunction func) {
            await SetChannel(func, Context.Channel);
        }
    }

    public class ServerCommands : ModuleBase {
        [Command("setprefix")]
        [Summary("Назначает префикс для команд бота")]
        public async Task SetPrefix([Summary("Префикс")] string prefix) {
            GuildConfig.Get(Context.Guild.Id).SetServerPrefix(prefix).Save();
            Context.Message.SafeDelete();
            (await Context.Channel.SendMessageAsync(
                    $"Успешно изменен префикс бота на `{prefix}`.\nЕсли вы забыли префикс, то просто упомяните бота.\n*Это сообщение будет удалено через минуту.")
                ).DelayedDelete(TimeSpan.FromMinutes(1));
        }
    }
}