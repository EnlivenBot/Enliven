using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bot.Config;
using Bot.DiscordRelated.Commands.Modules;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using HarmonyLib;
using NLog;

// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedMember.Local

// ReSharper disable InconsistentNaming

namespace Bot.DiscordRelated.Commands {
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

        private static bool _usersPatched;

        public static void ApplyUsersPatch() {
            if (_usersPatched) return;
            _usersPatched = true;
            logger.Info("Injecting into users creation");
            var usersCreationPostfix = new HarmonyMethod(AccessTools.FirstMethod(typeof(Patch), info => info.Name == "UsersCreationPostfix"));
            foreach (var constructor in CollectConstructors(typeof(SocketUser), typeof(RestUser))) {
                _harmony.Patch(constructor, null, usersCreationPostfix);
            }
            logger.Info("Injected into users creation");

            List<ConstructorInfo> CollectConstructors(params Type[] targetTypes) {
                return targetTypes.SelectMany(type => AccessTools.GetDeclaredConstructors(type)).ToList();
            }
        }

        static void UsersCreationPostfix(IUser __instance) {
            Task.Run(async () => {
                await Task.Delay(5000);
                UserData.MatchWithUser(__instance);
            });
        }
    }
}