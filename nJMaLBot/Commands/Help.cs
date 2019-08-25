using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Utilities;
using Discord;
using Discord.Commands;

namespace Bot.Commands
{
    class HelpUtils
    {
        public static List<EmbedFieldBuilder> BuildHelpField(string command) {
            List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>();
            foreach (var thisCommand in Program.Handler.AllCommands.Where(x => x.Aliases.Any(y => y == command))) {
                fields.Add(new EmbedFieldBuilder() {
                                                       Name = $"**{command}** - (Псевдонимы: `{string.Join("`,`", thisCommand.Aliases)}`)",
                                                       Value =
                                                           $"{thisCommand.Summary}\n```css\n&{thisCommand.Name} {(thisCommand.Parameters.Count == 0 ? "" : $"[{string.Join("] [", thisCommand.Parameters.Select(x => x.Name))}]")}```" +
                                                           (thisCommand.Parameters.Count == 0 ? "" : "\n" + string.Join("\n", thisCommand.Parameters.Select(x => $"`{x.Name}` - {x.Summary}")))
                                                   });
            }

            return fields;
        }

        public static void PrintHelpByCommand(ulong channelId, string command, string comment = "") {
            EmbedBuilder eb = new EmbedBuilder();
            eb.WithDescription(comment)
              .WithFields(BuildHelpField(command))
              .WithTitle($"Справка о команде `{command}`")
              .WithColor(Color.Gold);
            (Program.Client.GetChannel(channelId) as IMessageChannel)
               .SendMessageAsync("", false, eb.Build());
        }

        public static void PrintHelp(ulong channelId) {
            List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>();
            foreach (var thisCommand in Program.Handler.AllCommands) {
                fields.Add(new EmbedFieldBuilder() {
                                                       Name = $"**{thisCommand.Name}** {(thisCommand.Parameters.Count == 0 ? "" : $"[{string.Join("] [", thisCommand.Parameters.Select(x => x.Name))}]")} - (Псевдонимы: `{string.Join("`,`", thisCommand.Aliases)}`)",
                                                       Value = $"{thisCommand.Summary}"
                                                   });
            }

            //Program.Handler.AllCommands.GroupBy()
            EmbedBuilder eb = new EmbedBuilder();
            eb.WithTitle("Список доступных команд:")
              .WithColor(Color.Gold)
              .WithFields(fields);
            (Program.Client.GetChannel(channelId) as IMessageChannel)
               .SendMessageAsync("", false, eb.Build());
        }
    }

    public class HelpCommand : ModuleBase
    {
        [Command("help")]
        [Summary("Показывает информацию о всех командах")]
        public async Task PrintHelp() {
            await Context.Message.DeleteAsync();
            HelpUtils.PrintHelp(Context.Channel.Id);
        }

        [Command("help")]
        [Summary(                                        "Показывает информацию о определенной команде")]
        public async Task PrintHelp([Remainder] [Summary("Название команды")]
                                    string message) {
            HelpUtils.PrintHelpByCommand(Context.Channel.Id, message);
            await Context.Message.DeleteAsync();
        }
    }
}
