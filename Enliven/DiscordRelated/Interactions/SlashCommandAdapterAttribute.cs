using System;
using System.Linq;
using System.Reflection;
using Bot.DiscordRelated.Commands;
using Common;
using Common.Config;
using Discord.Commands;

namespace Bot.DiscordRelated.Interactions {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    sealed class SlashCommandAdapterAttribute : Attribute {
        public bool ProcessSlashCommand { get; }
        public SlashCommandAdapterAttribute(bool processSlashCommand = true) {
            ProcessSlashCommand = processSlashCommand;
        }
    }
}