using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules;
using Discord.Commands;
using HarmonyLib;
using NLog;

namespace Bot.Patches {
    public class CommandExecutionPatch : IPatch {
        private Harmony _patcher;
        private ILogger _logger;
        private static bool _isPatched;

        public CommandExecutionPatch(Harmony patcher, ILogger logger) {
            _logger = logger;
            _patcher = patcher;
        }

        public Task Apply() {
            if (_isPatched) return Task.CompletedTask;
            _isPatched = true;
            _logger.Info("Injecting into commands");
            var methods = Assembly.GetEntryAssembly()?.GetTypes()
                                  .SelectMany(t => t.GetMethods())
                                  .Where(m => m.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0)
                                  .ToList() ?? new List<MethodInfo>();

            var commandPostfix = new HarmonyMethod(AccessTools.FirstMethod(typeof(CommandExecutionPatch), info => info.Name == "CommandPostfix"));
            foreach (var methodInfo in methods) {
                _logger.Info("Patching {methodName}", methodInfo.Name);
                _patcher.Patch(methodInfo, null, commandPostfix);
            }
            
            return Task.CompletedTask;
        }
        
        void CommandPostfix(ref Task __result, PatchableModuleBase __instance) {
            if (__result.Exception != null) {
                _logger.Error(__result.Exception.Flatten(), "Command {commandName} execution failed with error", __instance.CurrentCommandInfo.Name);
            }
        }
    }
}