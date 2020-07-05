using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Utilities.Modules;
using Discord.Commands;
using HarmonyLib;
using NLog;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedMember.Local

// ReSharper disable InconsistentNaming

namespace Bot.Utilities.Commands {
    public class Patch {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Harmony _harmony = new Harmony("com.skproch.njmalbot.bot");
        private static bool _commandsPatched;

        public static void ApplyCommandPatch() {
            if (_commandsPatched) return;
            _commandsPatched = true;
            logger.Info("Injecting into commands");
            var methods = Assembly.GetEntryAssembly()?.GetTypes()
                                  .SelectMany(t => t.GetMethods())
                                  .Where(m => m.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0)
                                  .ToList() ?? new List<MethodInfo>();

            var commandPostfix = new HarmonyMethod(AccessTools.FirstMethod(typeof(Patch), info => info.Name == "CommandPostfix"));
            foreach (var methodInfo in methods) {
                logger.Info("Patching {methodName}", methodInfo.Name);
                _harmony.Patch(methodInfo, null, commandPostfix);
            }
        }

        static void CommandPostfix(ref Task __result, PatchableModuleBase __instance) {
            if (__result.Exception != null) {
                logger.Error(__result.Exception.Flatten(), "Command {commandName} execution failed with error", __instance.CurrentCommandInfo.Name);
            }
        }
    }
}