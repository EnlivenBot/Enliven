using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Discord;
using Discord.Commands;

namespace Bot.Commands {
    public class HelpCommand : AdvancedModuleBase {
        [Command("help")]
        [Summary("help1s")]
        public async Task PrintHelp() {
            var eb = new EmbedBuilder();
            eb.WithTitle(Loc.Get("Help.HelpTitle"))
              .WithColor(Color.Gold)
              .AddField($"{GuildConfig.Prefix}help", Loc.Get("Help.HelpDescription"))
              .WithFooter(Context.User.Username, Context.User.GetAvatarUrl())
              .WithFields(HelpUtils.CommandsGroups.Value.Select(pair =>
                   new EmbedFieldBuilder {
                       Name = pair.Value.GroupNameTemplate.Format(Loc.Get($"Groups.{pair.Key}"), GuildConfig.Prefix),
                       Value = pair.Value.GroupTextTemplate.Format(GuildConfig.Prefix)
                   }));
            (await (await GetResponseChannel()).SendMessageAsync(null, false, eb.Build())).DelayedDelete(TimeSpan.FromMinutes(10));
        }

        [Command("help")]
        [Summary("help0s")]
        public async Task PrintHelp([Remainder] [Summary("help0_0s")] string message) {
            var eb = new EmbedBuilder()
                    .WithFooter(Context.User.Username, Context.User.GetAvatarUrl())
                    .WithColor(Color.Gold);
            if (HelpUtils.CommandsGroups.Value.TryGetValue(message, out var commandGroup)) {
                eb.WithTitle(Loc.Get("Help.CommandsOfGroup").Format(message))
                  .WithFields(commandGroup.Commands.GroupBy(info => info.Summary).Select(infos => infos.First()).Select(info => new EmbedFieldBuilder {
                       Name = $"{GuildConfig.Prefix}{info.Name} {HelpUtils.GetAliasesString(info.Aliases, Loc)}",
                       Value = Loc.Get($"Help.{info.Summary}")
                   }));
            }
            else if (HelpUtils.CommandAliases.Value.Contains(message)) {
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