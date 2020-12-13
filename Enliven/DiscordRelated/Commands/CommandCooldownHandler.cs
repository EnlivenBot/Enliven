using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Bot.Utilities;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands {
    public class CommandCooldownHandler {
        private ConcurrentDictionary<(ulong, CommandInfo), DateTime> GuildCooldown = new ConcurrentDictionary<(ulong, CommandInfo), DateTime>();
        private ConcurrentDictionary<(ulong, CommandInfo), DateTime> ChannelCooldown = new ConcurrentDictionary<(ulong, CommandInfo), DateTime>();
        private ConcurrentDictionary<(ulong, CommandInfo), DateTime> UserCooldown = new ConcurrentDictionary<(ulong, CommandInfo), DateTime>();

        private static bool CanExecute(ConcurrentDictionary<(ulong, CommandInfo), DateTime> target, ulong? targetId, CommandInfo commandInfo,
                                       TimeSpan? cooldown) {
            if (cooldown == null || targetId == null) return true;
            var id = (ulong) targetId;
            if (target.TryGetValue((id, commandInfo), out var lastExecution)) {
                if (DateTime.Now < lastExecution + cooldown) {
                    return false;
                }
            }

            return true;
        }

        private static void RegisterExecution(ConcurrentDictionary<(ulong, CommandInfo), DateTime> target, ulong? targetId, CommandInfo commandInfo,
                                              TimeSpan? cooldown) {
            if (cooldown == null || targetId == null) return;
            var id = (ulong) targetId;
            target[(id, commandInfo)] = DateTime.Now;
        }

        public bool IsCommandOnCooldown(CommandInfo command, ICommandContext context) {
            var cooldown = command.GetCooldown();
            return cooldown != null && IsCommandOnCooldownInternal(command, context, cooldown);
        }

        private bool IsCommandOnCooldownInternal(CommandInfo command, ICommandContext context, CommandCooldownAttribute cooldown) {
            var isCommandOnCooldown = !(CanExecute(UserCooldown, context.User?.Id, command, cooldown.UserDelay)
                                     && CanExecute(ChannelCooldown, context.Channel?.Id, command, cooldown.ChannelDelay)
                                     && CanExecute(GuildCooldown, context.Guild?.Id, command, cooldown.GuildDelay));
            if (isCommandOnCooldown) return isCommandOnCooldown;
            
            RegisterExecution(UserCooldown, context.User?.Id, command, cooldown.UserDelay);
            RegisterExecution(ChannelCooldown, context.Channel?.Id, command, cooldown.ChannelDelay);
            RegisterExecution(GuildCooldown, context.Guild?.Id, command, cooldown.GuildDelay);
            return isCommandOnCooldown;
        }
    }
}