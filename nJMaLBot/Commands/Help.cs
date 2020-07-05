using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Bot.Utilities.Modules;
using Discord;
using Discord.Commands;

namespace Bot.Commands {
    public class HelpCommand : AdvancedModuleBase {
        [Command("help")]
        [Summary("help1s")]
        public async Task PrintHelp() {
            var eb = this.GetAuthorEmbedBuilder()
                         .WithTitle(Loc.Get("Help.HelpTitle"))
                         .WithColor(Color.Gold)
                         .WithDescription(Loc.Get("Help.HelpPrefix").Format(GuildConfig.Prefix, Program.Client.CurrentUser.Mention))
                         .AddField($"{GuildConfig.Prefix}help", Loc.Get("Help.HelpDescription"))
                         .WithFields(HelpUtils.CommandsGroups.Value.Select(pair =>
                              new EmbedFieldBuilder {
                                  Name = pair.Value.GroupNameTemplate.Format(Loc.Get($"Groups.{pair.Key}"), GuildConfig.Prefix),
                                  Value = pair.Value.GroupTextTemplate.Format(GuildConfig.Prefix)
                              }));
            eb.AddField(Loc.Get("Common.Vote"), Loc.Get("Common.VoteDescription"));
            (await (await GetResponseChannel()).SendMessageAsync(null, false, eb.Build())).DelayedDelete(TimeSpan.FromMinutes(10));
        }

        [Command("help")]
        [Summary("help0s")]
        public async Task PrintHelp([Remainder] [Summary("help0_0s")] string message) {
            var eb = this.GetAuthorEmbedBuilder().WithColor(Color.Gold);
            if (HelpUtils.CommandsGroups.Value.TryGetValue(message, out var commandGroup)) {
                eb.WithTitle(Loc.Get("Help.CommandsOfGroup").Format(message))
                  .WithFields(commandGroup.Commands.GroupBy(info => info.Summary).Select(infos => infos.First()).Select(info => new EmbedFieldBuilder {
                       Name = $"{GuildConfig.Prefix}{info.Name} {HelpUtils.GetAliasesString(info.Aliases, Loc)}",
                       Value = Loc.Get($"Help.{info.Summary}")
                   }));
            }
            else if (Program.Handler.CommandAliases.Contains(message)) {
                eb.WithTitle(Loc.Get("Help.ByCommand").Format(message))
                  .WithFields(HelpUtils.BuildHelpFields(message, GuildConfig.Prefix, Loc));
            }
            else {
                eb.WithTitle(Loc.Get("Help.NotFoundTitle"))
                  .WithDescription(Loc.Get("Help.NotFoundDescription").Format(message.SafeSubstring(0, 1900), GuildConfig.Prefix));
            }

            (await (await GetResponseChannel()).SendMessageAsync(null, false, eb.Build())).DelayedDelete(TimeSpan.FromMinutes(10));
        }
    }
}