using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Common.Config;

namespace Bot.DiscordRelated.Commands {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    sealed class RegisterIf : Attribute, RegisterIf.IRegisterIfChecker {
        private static ConcurrentDictionary<Type, IRegisterIfChecker> _checkers = new();
        private Type _funcType;
        public RegisterIf(Type funcType) {
            _funcType = funcType;
            if (!typeof(IRegisterIfChecker).IsAssignableFrom(funcType)) {
                throw new ArgumentException("Fync type class must be inherited from IRegisterIfChecker", nameof(funcType));
            }
        }
        
        public bool CanBeRegistered(GlobalConfig globalConfig, InstanceConfig instanceConfig, Type moduleType) {
            var checker = _checkers.GetOrAdd(_funcType, type => Activator.CreateInstance(type) as IRegisterIfChecker ?? throw new InvalidOperationException($"Cannot create checker of type {type.FullName}"));
            return checker.CanBeRegistered(globalConfig, instanceConfig, moduleType);
        }
        
        public interface IRegisterIfChecker {
            public bool CanBeRegistered(GlobalConfig globalConfig, InstanceConfig instanceConfig, Type moduleType);
        }
        
        public class LoggingEnabled : IRegisterIfChecker {
            public bool CanBeRegistered(GlobalConfig globalConfig, InstanceConfig instanceConfig, Type moduleType) {
                return instanceConfig.IsLoggingEnabled();
            }
        }
    }
}