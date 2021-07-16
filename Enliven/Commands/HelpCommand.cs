using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.Utilities;
using Common;
using Discord;
using Discord.Commands;

namespace Bot.Commands {
    public class HelpCommand : AdvancedModuleBase {
        public CommandHandlerService CommandHandlerService { get; set; } = null!;
        public CustomCommandService CustomCommandService { get; set; } = null!;

        [Command("help")]
        [Summary("help1s")]
        public async Task PrintHelp() {
            var eb = this.GetAuthorEmbedBuilder()
                         .WithTitle(Loc.Get("Help.HelpTitle"))
                         .WithColor(Color.Gold)
                         .WithDescription(Loc.Get("Help.HelpPrefix").Format(GuildConfig.Prefix, EnlivenBot.Client.CurrentUser.Mention))
                         .AddField($"{GuildConfig.Prefix}help", Loc.Get("Help.HelpDescription"))
                         .WithFields(CustomCommandService.CommandsGroups.Value.Select(pair =>
                              new EmbedFieldBuilder {
                                  Name = pair.Value.GroupNameTemplate.Format(Loc.Get($"Groups.{pair.Key}"), GuildConfig.Prefix),
                                  Value = pair.Value.GroupTextTemplate.Format(GuildConfig.Prefix)
                              }));
            eb.AddField(Loc.Get("Common.Vote"), Loc.Get("Common.VoteDescription"));
            var responseChannel = await GetResponseChannel();
            _ = responseChannel.SendMessageAsync(null, false, eb.Build()).DelayedDelete(Constants.LongTimeSpan);
        }

        [Command("help")]
        [Summary("help0s")]
        public async Task PrintHelp([Remainder] [Summary("help0_0s")] string message) {
            var eb = this.GetAuthorEmbedBuilder().WithColor(Color.Gold);
            if (CustomCommandService.CommandsGroups.Value.TryGetValue(message, out var commandGroup)) {
                eb.WithTitle(Loc.Get("Help.CommandsOfGroup").Format(message))
                  .WithFields(commandGroup.Commands.GroupBy(info => info.Name).Select(infos => infos.First()).Select(info => new EmbedFieldBuilder {
                       Name = $"`{GuildConfig.Prefix}{info.Name}` {CustomCommandService.GetAliasesString(info.Aliases, Loc)}",
                       Value = Loc.Get($"Help.{info.Summary}")
                   }));
            }
            else if (CustomCommandService.Aliases.Contains(message)) {
                eb.WithTitle(Loc.Get("Help.ByCommand").Format(message))
                  .WithFields(CustomCommandService.BuildHelpFields(message, GuildConfig.Prefix, Loc));
            }
            else {
                eb.WithTitle(Loc.Get("Help.NotFoundTitle"))
                  .WithDescription(Loc.Get("Help.NotFoundDescription").Format(message.SafeSubstring(100, "..."), GuildConfig.Prefix));
            }

            var responseChannel = await GetResponseChannel();
            _ = responseChannel.SendMessageAsync(null, false, eb.Build()).DelayedDelete(Constants.LongTimeSpan);
        }
    }
}