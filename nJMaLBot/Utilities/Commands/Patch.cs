using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Music.Players;
using Bot.Utilities.Modules;
using Discord.Commands;
using HarmonyLib;
using Lavalink4NET.Player;

// ReSharper disable InconsistentNaming

namespace Bot.Utilities.Commands {
    public class Patch {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static void ApplyPatch() {
            var harmony = new Harmony("com.skproch.njmalbot.bot");

            logger.Info("Injecting into commands");
            var methods = Assembly.GetEntryAssembly()?.GetTypes()
                                  .SelectMany(t => t.GetMethods())
                                  .Where(m => m.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0)
                                  .ToList() ?? new List<MethodInfo>();

            var commandPostfix = new HarmonyMethod(AccessTools.FirstMethod(typeof(Patch), info => info.Name == "CommandPostfix"));
            foreach (var methodInfo in methods) {
                logger.Info("Patching {methodName}", methodInfo.Name);
                harmony.Patch(methodInfo, null, commandPostfix, null, null);
            }
        }

        static void CommandPostfix(ref Task __result, PatchableModuleBase __instance) {
            if (__result.Exception != null) {
                logger.Error(__result.Exception.Flatten(), "Command {commandName} execution failed with error", __instance.CurrentCommandInfo.Name);
            }
        }
    }
}