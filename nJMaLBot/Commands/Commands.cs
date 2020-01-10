using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NeoSmart.Unicode;

namespace Bot.Commands {
    public class GetLogByMessage : AdvancedModuleBase {
        [Command("history", RunMode = RunMode.Async)]
        [Summary("Показывает историю изменений сообщения.\n" +
                 "Так-же можно поставить эмоцию 📖 под нужное сообщение")]
        public async Task PrintChanges(
            [Remainder] [Summary("ID сообщения, чью историю нужно вывести. Так-же можно использовать пару `ID канала`-`ID сообщения`")]
            string id) {
            var channelId = Context.Channel.Id;
            var messageId = id;
            if (id.Contains('-')) {
                channelId = Convert.ToUInt64(id.Split('-')[0]);
                messageId = id.Split('-')[1];
            }

            await MessageHistoryManager.PrintLog(Convert.ToUInt64(messageId), channelId, (SocketTextChannel) Context.Channel, (IGuildUser) Context.User);
        }
    }

    [RequireUserPermission(GuildPermission.Administrator)]
    public class ChannelsCommands : AdvancedModuleBase {
        [Command("setchannel")]
        [Summary("Назначает канал, который будет использоваться ботом для определенного типа сообщений")]
        public async Task SetChannel([Summary("Название функции канала (`music`, `log`)")]
                                     ChannelFunction func, [Summary("Ссылка на канал")] IChannel channel) {
            GuildConfig.Get(Context.Guild.Id).SetChannel(channel.Id.ToString(), func).Save();
            await (await GetResponseChannel()).SendMessageAsync(Loc.Get("Commands.SetChannelResponse").Format(channel.Id, func.ToString()));
            Context.Message.SafeDelete();
        }

        [Command("setchannel")]
        [Summary("Назначает канал, который будет использоваться ботом для определенного типа сообщений")]
        public async Task SetThisChannel([Summary("Ссылка на канал")] ChannelFunction func) {
            await SetChannel(func, Context.Channel);
        }
    }

    [RequireUserPermission(GuildPermission.Administrator)]
    public class ServerCommands : AdvancedModuleBase {
        [Command("setprefix")]
        [Summary("Назначает префикс для команд бота")]
        public async Task SetPrefix([Summary("Префикс")] string prefix) {
            GuildConfig.Get(Context.Guild.Id).SetServerPrefix(prefix).Save();
            await (await GetResponseChannel()).SendMessageAsync(Loc.Get("Commands.SetPrefixResponse").Format(prefix));
            Context.Message.SafeDelete();
        }

        [Command("setlanguage")]
        [Summary("Назначает язык ответов бота")]
        public async Task SetLanguage([Summary("Язык")] string language) {
            if (Localization.Languages.ContainsKey(language)) {
                GuildConfig.Get(Context.Guild.Id).SetLanguage(language).Save();
                Context.Message.SafeDelete();
                (await (await GetResponseChannel()).SendMessageAsync(Localization.Get(Context.Guild.Id, "Localization.Success").Format(language)))
                   .DelayedDelete(
                        TimeSpan.FromMinutes(1));
            }
            else {
                (await (await GetResponseChannel()).SendMessageAsync(Localization.Get(Context.Guild.Id, "Localization.Fail")
                                                                                 .Format(language,
                                                                                      string.Join(' ', Localization.Languages.Select(pair => $"`{pair.Key}`"))))
                    ).DelayedDelete(TimeSpan.FromMinutes(1));
            }
        }
    }
}