using System;

namespace Bot.DiscordRelated.Commands {
    [AttributeUsage(AttributeTargets.All)]
    public sealed class CommandCooldownAttribute : Attribute {
        public CommandCooldownAttribute(double userDelayMilliseconds) {
            UserDelayMilliseconds = userDelayMilliseconds;
        }

        public CommandCooldownAttribute() { }
        public double UserDelayMilliseconds { get; set; }
        public double ChannelDelayMilliseconds { get; set; }
        public double GuildDelayMilliseconds { get; set; }
        public TimeSpan? UserDelay => TimeSpan.FromMilliseconds(UserDelayMilliseconds);
        public TimeSpan? ChannelDelay => TimeSpan.FromMilliseconds(ChannelDelayMilliseconds);
        public TimeSpan? GuildDelay => TimeSpan.FromMilliseconds(GuildDelayMilliseconds);
    }
}