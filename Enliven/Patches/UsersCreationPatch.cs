using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Common.Config;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using HarmonyLib;
using NLog;

namespace Bot.Patches {
    public class UsersCreationPatch : IPatch {
        private Harmony _patcher;
        private ILogger _logger;
        private IUserDataProvider _userDataProvider;
        private static bool _isPatched;

        public UsersCreationPatch(Harmony patcher, ILogger logger, IUserDataProvider userDataProvider) {
            _userDataProvider = userDataProvider;
            _logger = logger;
            _patcher = patcher;
        }
        
        public Task Apply() {
            if (_isPatched) return Task.CompletedTask;
            _isPatched = true;
            _logger.Info("Injecting into users creation");
            var usersCreationPostfix = new HarmonyMethod(AccessTools.FirstMethod(typeof(Patch), info => info.Name == "UsersCreationPostfix"));
            foreach (var constructor in CollectConstructors(typeof(SocketUser), typeof(RestUser))) {
                _patcher.Patch(constructor, null, usersCreationPostfix);
            }
            _logger.Info("Injected into users creation");

            List<ConstructorInfo> CollectConstructors(params Type[] targetTypes) {
                return targetTypes.SelectMany(type => AccessTools.GetDeclaredConstructors(type)).ToList();
            }
            
            return Task.CompletedTask;
        }
        
        void UsersCreationPostfix(IUser __instance) {
            Task.Run(async () => {
                await Task.Delay(5000);
                _userDataProvider.MatchWithUser(__instance);
            });
        }
    }
}