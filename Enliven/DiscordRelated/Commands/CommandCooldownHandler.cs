using System;
using System.Collections.Concurrent;
using Bot.DiscordRelated.Commands.Attributes;
using Bot.Utilities;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands;

public class CommandCooldownHandler
{
    private ConcurrentDictionary<(ulong, CommandInfo), DateTime> _channelCooldown = new();
    private ConcurrentDictionary<(ulong, CommandInfo), DateTime> _guildCooldown = new();
    private ConcurrentDictionary<(ulong, CommandInfo), DateTime> _userCooldown = new();

    private static bool CanExecute(ConcurrentDictionary<(ulong, CommandInfo), DateTime> target, ulong? targetId,
        CommandInfo commandInfo,
        TimeSpan? cooldown)
    {
        if (cooldown == null || targetId == null) return true;
        var id = (ulong)targetId;
        if (target.TryGetValue((id, commandInfo), out var lastExecution))
        {
            if (DateTime.Now < lastExecution + cooldown) return false;
        }

        return true;
    }

    private static void RegisterExecution(ConcurrentDictionary<(ulong, CommandInfo), DateTime> target, ulong? targetId,
        CommandInfo commandInfo,
        TimeSpan? cooldown)
    {
        if (cooldown == null || targetId == null) return;
        var id = (ulong)targetId;
        target[(id, commandInfo)] = DateTime.Now;
    }

    public bool IsCommandOnCooldown(CommandInfo command, ICommandContext context)
    {
        var cooldown = command.GetCooldown();
        return cooldown != null && IsCommandOnCooldownInternal(command, context, cooldown);
    }

    private bool IsCommandOnCooldownInternal(CommandInfo command, ICommandContext context,
        CommandCooldownAttribute cooldown)
    {
        var isCommandOnCooldown = !(CanExecute(_userCooldown, context.User?.Id, command, cooldown.UserDelay)
                                    && CanExecute(_channelCooldown, context.Channel?.Id, command, cooldown.ChannelDelay)
                                    && CanExecute(_guildCooldown, context.Guild?.Id, command, cooldown.GuildDelay));
        if (isCommandOnCooldown) return isCommandOnCooldown;

        RegisterExecution(_userCooldown, context.User?.Id, command, cooldown.UserDelay);
        RegisterExecution(_channelCooldown, context.Channel?.Id, command, cooldown.ChannelDelay);
        RegisterExecution(_guildCooldown, context.Guild?.Id, command, cooldown.GuildDelay);
        return isCommandOnCooldown;
    }
}