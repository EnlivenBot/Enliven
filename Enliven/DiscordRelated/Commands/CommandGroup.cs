using System.Collections.Generic;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands {
    public class CommandGroup {
        public string GroupId { get; set; } = null!;
        public string GroupNameTemplate { get; set; } = null!;
        public string GroupTextTemplate { get; set; } = null!;
        public List<CommandInfo> Commands { get; set; } = null!;
    }
}