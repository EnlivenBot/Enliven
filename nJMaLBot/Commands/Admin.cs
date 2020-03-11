using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Bot.Utilities.Modules;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Commands {
    [Grouping("admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class AdminCommands : AdvancedModuleBase {
        [Command("setprefix")]
        [Summary("setprefix0s")]
        public async Task SetPrefix([Summary("setrefix0_0s")] string prefix) {
            GuildConfig.Get(Context.Guild.Id).SetServerPrefix(prefix).Save();
            await (await GetResponseChannel()).SendMessageAsync(Loc.Get("Commands.SetPrefixResponse").Format(prefix));
            Context.Message.SafeDelete();
        }

        [Command("setlanguage")]
        [Summary("setlanguage0s")]
        public async Task SetLanguage([Summary("setlanguage0_0s")] string language) {
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

        [Command("setchannel")]
        [Summary("setchannel0s")]
        public async Task SetChannel([Summary("setchannel0_0s")] ChannelFunction func,
                                     [Summary("setchannel0_1s")] IChannel channel) {
            GuildConfig.Get(Context.Guild.Id).SetChannel(channel.Id.ToString(), func).Save();
            await (await GetResponseChannel()).SendMessageAsync(Loc.Get("Commands.SetChannelResponse").Format(channel.Id, func.ToString()));
            Context.Message.SafeDelete();
        }

        [Command("setchannel")]
        [Summary("setchannel0s")]
        public async Task SetThisChannel([Summary("setchannel0_1s")] ChannelFunction func) {
            await SetChannel(func, Context.Channel);
        }
        
        [Command("clearhistories", RunMode = RunMode.Async)]
        [Summary("clearhistories0s")]
        public async Task ClearHistories() {
            await ReplyAsync("Start clearing message histories");
            await MessageHistoryManager.ClearGuildLogs((SocketGuild) Context.Guild);
        }
    }
}