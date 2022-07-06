using System.Linq;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Common.Localization.Providers;
using Discord;
using Discord.Commands;

namespace Bot.Utilities {
    public static class ExtensionMethods {
        public static GroupingAttribute? GetGroup(this CommandInfo info) {
            return (info.Attributes.FirstOrDefault(attribute => attribute is GroupingAttribute) ??
                    info.Module.Attributes.FirstOrDefault(attribute => attribute is GroupingAttribute)) as GroupingAttribute;
        }

        public static string GetLocalizedName(this GroupingAttribute? groupingAttribute, ILocalizationProvider loc) {
            return loc.Get($"Groups.{groupingAttribute?.GroupName ?? ""}");
        }

        public static bool IsHiddenCommand(this CommandInfo info) {
            return (info.Attributes.FirstOrDefault(attribute => attribute is HiddenAttribute) ??
                    info.Module.Attributes.FirstOrDefault(attribute => attribute is HiddenAttribute)) != null;
        }
        
        public static EmbedBuilder GetAuthorEmbedBuilder(this AdvancedModuleBase moduleBase) {
            return DiscordUtils.GetAuthorEmbedBuilder(moduleBase.Context.User, moduleBase.Loc);
        }

        public static CommandCooldownAttribute? GetCooldown(this CommandInfo info) {
            return (CommandCooldownAttribute?) info.Attributes.FirstOrDefault(attribute => attribute is CommandCooldownAttribute);
        }
    }
}