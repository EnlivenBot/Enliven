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
    public class CommandsPatch {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static void ApplyPatch() {
            logger.Info("Injecting into commands");
            var methods = Assembly.GetEntryAssembly()?.GetTypes()
                                  .SelectMany(t => t.GetMethods())
                                  .Where(m => m.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0)
                                  .ToList() ?? new List<MethodInfo>();

            var harmony = new Harmony("com.skproch.njmalbot.bot");
            var commandPostfix = new HarmonyMethod(AccessTools.FirstMethod(typeof(CommandsPatch), info => info.Name == "Postfix"));
            foreach (var methodInfo in methods) {
                logger.Info("Patching {methodName}", methodInfo.Name);
                harmony.Patch(methodInfo, null, commandPostfix, null, null);
            }

            harmony.PatchAll();
        }

        static void Postfix(ref Task __result, PatchableModuleBase __instance) {
            if (__result.Exception != null) {
                logger.Error(__result.Exception.Flatten(), "Command {commandName} execution failed with error", __instance.CurrentCommandInfo.Name);
            }
        }
    }

    [HarmonyPatch(typeof(LavalinkPlayer))]
    public class LavalinkPlayerPatch {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LavalinkPlayer), "State", MethodType.Setter)]
        static void OnStateChanged(LavalinkPlayer __instance) {
            if (__instance is AdvancedLavalinkPlayer player) {
                player.OnStateChanged();
            }
        }
    }

    [HarmonyPatch(typeof(AdvancedLavalinkPlayer))]
    public class PlaylistLavalinkPlayerPatch {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AdvancedLavalinkPlayer), "IsExternalEmojiAllowed", MethodType.Setter)]
        static void OnRepeatStateChanged(AdvancedLavalinkPlayer __instance) {
            if (__instance is PlaylistLavalinkPlayer player) {
                player.OnRepeatStateChanged();
                player.OnStateChanged();
            }
        }
    }
}