using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Config.Localization.Providers;
using Discord;
using Discord.Commands;

namespace Bot.Utilities.Commands {
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

        public static readonly Lazy<Lookup<string, CommandInfo>> CommandAliases = new Lazy<Lookup<string, CommandInfo>>(() => {
            var items = new List<KeyValuePair<string, CommandInfo>>();
            foreach (var command in Program.Handler.AllCommands) {
                items.AddRange(command.Aliases.Select(alias => new KeyValuePair<string, CommandInfo>(alias, command)));
            }

            return (Lookup<string, CommandInfo>) items.ToLookup(pair => pair.Key, pair => pair.Value);
        });

        public static IEnumerable<EmbedFieldBuilder> BuildHelpFields(string command, string prefix, ILocalizationProvider loc) {
            return CommandAliases.Value[command].Select(info => new EmbedFieldBuilder {
                Name = loc.Get("Help.CommandTitle").Format(command, GetAliasesString(info.Aliases, loc)),
                Value = $"{loc.Get($"Help.{info.Summary}")}\n" +
                        $"```css\n" +
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
            if (!enumerable.Any())
                return "";

            return "(" + loc.Get("Help.Aliases") + GetAliases(enumerable) + ")";
        }

        private static string GetAliases(IEnumerable<string> aliases) {
            var s = new StringBuilder();
            foreach (var aliase in aliases) {
                s.Append($" `{aliase}` ");
            }

            return s.ToString().Trim();
        }
    }

    public class CommandGroup {
        public string GroupId { get; set; }
        public string GroupNameTemplate { get; set; }
        public string GroupTextTemplate { get; set; }
        public List<CommandInfo> Commands { get; set; }
    }
}