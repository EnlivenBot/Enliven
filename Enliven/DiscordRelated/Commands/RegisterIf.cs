using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Common.Config;

namespace Bot.DiscordRelated.Commands {
    [AttributeUsage(AttributeTargets.Class)]
    sealed class RegisterIf : Attribute, RegisterIf.IRegisterIfChecker {
        private static ConcurrentDictionary<Type, IRegisterIfChecker> _checkers = new();
        private Type _funcType;
        public RegisterIf(Type funcType) {
            _funcType = funcType;
            if (!typeof(IRegisterIfChecker).IsAssignableFrom(funcType)) {
                throw new ArgumentException("Fync type class must be inherited from IRegisterIfChecker", nameof(funcType));
            }
        }

        public static bool ShouldRegisterType(Type type, GlobalConfig globalConfig, InstanceConfig instanceConfig) {
            return type.GetCustomAttribute<RegisterIf>()?.CanBeRegistered(globalConfig, instanceConfig) ?? true;
        }
        
        public bool CanBeRegistered(GlobalConfig globalConfig, InstanceConfig instanceConfig) {
            var checker = _checkers.GetOrAdd(_funcType, type => Activator.CreateInstance(type) as IRegisterIfChecker ?? throw new InvalidOperationException($"Cannot create checker of type {type.FullName}"));
            return checker.CanBeRegistered(globalConfig, instanceConfig);
        }
        
        public interface IRegisterIfChecker {
            public bool CanBeRegistered(GlobalConfig globalConfig, InstanceConfig instanceConfig);
        }
        
        public class LoggingEnabled : IRegisterIfChecker {
            public bool CanBeRegistered(GlobalConfig globalConfig, InstanceConfig instanceConfig) {
                return instanceConfig.IsLoggingEnabled();
            }
        }
    }
}