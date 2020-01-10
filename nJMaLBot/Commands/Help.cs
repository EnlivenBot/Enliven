using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Bot.Commands {
    internal static class HelpUtils {
        public static IEnumerable<EmbedFieldBuilder> BuildHelpField(string command) {
            return Program.Handler.AllCommands
                          .Where(x => x.Aliases.Any(y => y == command))
                          .Select(thisCommand => new EmbedFieldBuilder {
                               Name = $"**{command}** - (Псевдонимы: `{string.Join("`,`", thisCommand.Aliases)}`)",
                               Value =
                                   $"{thisCommand.Summary}\n```css\n&{thisCommand.Name} {(thisCommand.Parameters.Count == 0 ? "" : $"[{string.Join("] [", thisCommand.Parameters.Select(x => x.Name))}]")}```" +
                                   (thisCommand.Parameters.Count == 0
                                       ? ""
                                       : "\n" + string.Join("\n", thisCommand.Parameters.Select(x => $"`{x.Name}` - {x.Summary}")))
                           });
        }

        public static void PrintHelpByCommand(ulong channelId, string command, string comment = "") {
            var eb = new EmbedBuilder();

            eb.WithDescription(comment)
              .WithFields(BuildHelpField(command))
              .WithTitle($"Справка о команде `{command}`")
              .WithColor(Color.Gold);

            (Program.Client.GetChannel(channelId) as IMessageChannel)?.SendMessageAsync("", false, eb.Build());
        }

        public static void PrintHelp(ulong channelId) {
            var fields = Program.Handler.AllCommands
                                .Select(thisCommand => new EmbedFieldBuilder {
                                     Name =
                                         $"**{thisCommand.Name}** {(thisCommand.Parameters.Count == 0 ? "" : $"[{string.Join("] [", thisCommand.Parameters.Select(x => x.Name))}]")} - (Псевдонимы: `{string.Join("`,`", thisCommand.Aliases)}`)",
                                     Value = $"{thisCommand.Summary}"
                                 }).ToList();

            //Program.Handler.AllCommands.GroupBy()
            var eb = new EmbedBuilder();

            eb.WithTitle("Список доступных команд:")
              .WithColor(Color.Gold)
              .WithFields(fields);

            (Program.Client.GetChannel(channelId) as IMessageChannel)?.SendMessageAsync("", false, eb.Build());
        }
    }

    public class HelpCommand : AdvancedModuleBase {
        [Command("help")]
        [Summary("Показывает информацию о всех командах")]
        public async Task PrintHelp() {
            await Context.Message.DeleteAsync();
            HelpUtils.PrintHelp(Context.Channel.Id);
        }

        [Command("help")]
        [Summary("Показывает информацию о определенной команде")]
        public async Task PrintHelp([Remainder] [Summary("Название команды")]
                                    string message) {
            HelpUtils.PrintHelpByCommand(Context.Channel.Id, message);
            await Context.Message.DeleteAsync();
        }
    }
}