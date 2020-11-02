using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bot.Utilities;
using Common;
using Common.Localization.Providers;
using Discord;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands {
    internal static class HelpUtils {
        public static readonly Lazy<Dictionary<string, CommandGroup>> CommandsGroups = new Lazy<Dictionary<string, CommandGroup>>(() => {
            return Program.Handler.AllCommands.Where(info => !info.IsHiddenCommand())
                          .GroupBy(info => info.GetGroup()?.GroupName ?? "")
                          .Where(grouping => !string.IsNullOrWhiteSpace(grouping.Key)).Select(infos =>
                               new CommandGroup {
                                   Commands = infos.ToList(), GroupId = infos.Key,
                                   GroupNameTemplate = $"{{0}} ({{1}}help {infos.Key}):",
                                   GroupTextTemplate = string.Join(' ', infos.Select(info => info.Name)
                                                                             .GroupBy(s => s).Select(grouping => grouping.First())
                                                                             .Select(s => $"`{{0}}{s}`"))
                               }).ToDictionary(group => group.GroupId);
        });

        public static IEnumerable<EmbedFieldBuilder> BuildHelpFields(string command, string prefix, ILocalizationProvider loc) {
            return Program.Handler.CommandAliases[command].Select(info => new EmbedFieldBuilder {
                Name = loc.Get("Help.CommandTitle").Format(command, GetAliasesString(info.Aliases, loc)),
                Value = $"{loc.Get($"Help.{info.Summary}")}\n" +
                        "```css\n" +
                        $"{prefix}{info.Name} {(info.Parameters.Count == 0 ? "" : $"[{string.Join("] [", info.Parameters.Select(x => x.Name))}]")}```" +
                        (info.Parameters.Count == 0
                            ? ""
                            : "\n" + string.Join("\n",
                                info.Parameters.Select(x => $"`{x.Name}` - {(string.IsNullOrWhiteSpace(x.Summary) ? "" : loc.Get("Help." + x.Summary))}")))
            });
        }

        public static string GetAliasesString(IEnumerable<string> aliases, ILocalizationProvider loc, bool skipFirst = true) {
            aliases = skipFirst ? aliases.Skip(1) : aliases;
            var enumerable = aliases.ToList();
            return !enumerable.Any() ? "" : $"({GetAliases(enumerable)})";
        }

        private static string GetAliases(IEnumerable<string> aliases) {
            var s = new StringBuilder();
            foreach (var alias in aliases) {
                s.Append($" `{alias}` ");
            }

            return s.ToString().Trim();
        }
    }

    public class CommandGroup {
        public string GroupId { get; set; } = null!;
        public string GroupNameTemplate { get; set; } = null!;
        public string GroupTextTemplate { get; set; } = null!;
        public List<CommandInfo> Commands { get; set; } = null!;
    }
}