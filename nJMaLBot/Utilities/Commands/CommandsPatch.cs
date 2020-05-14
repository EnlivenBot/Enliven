using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bot.Utilities.Modules;
using Discord.Commands;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Bot.Utilities.Commands {
    public class CommandsPatch {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static void ApplyPatch() {
            logger.Info("Injecting into commands");
            var methods = Assembly.GetEntryAssembly()?.GetTypes()
                                  .SelectMany(t => t.GetMethods())
                                  .Where(m => m.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0)
                                  .ToList() ?? new List<MethodInfo>();

            var harmony = new Harmony("com.skproch.njmalbot.bot");
            var commandFinalizer = new HarmonyMethod(AccessTools.FirstMethod(typeof(CommandsPatch), info => info.Name == "Finalizer"));
            foreach (var methodInfo in methods) {
                logger.Info("Patching {methodName}", methodInfo.Name);
                harmony.Patch(methodInfo, null, null, null, commandFinalizer);
            }
        }

        static void Finalizer(Exception __exception, PatchableModuleBase __instance) {
            if (__exception != null) {
                logger.Error(__exception, "Command {commandName} execution failed with error", __instance.CurrentCommandInfo.Name);
            }
        }
    }
}