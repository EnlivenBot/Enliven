using System;
using System.Collections.Concurrent;
using System.Reflection;
using Common.Config;

namespace Bot.DiscordRelated.Commands;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class RegisterIf : Attribute, RegisterIf.IRegisterIfChecker {
    private static ConcurrentDictionary<Type, IRegisterIfChecker> _checkers = new();
    private Type _funcType;
    public RegisterIf(Type funcType) {
        _funcType = funcType;
        if (!typeof(IRegisterIfChecker).IsAssignableFrom(funcType)) throw new ArgumentException("Fync type class must be inherited from IRegisterIfChecker", nameof(funcType));
    }

    public bool CanBeRegistered(InstanceConfig instanceConfig) {
        var checker = _checkers.GetOrAdd(_funcType, type => Activator.CreateInstance(type) as IRegisterIfChecker ?? throw new InvalidOperationException($"Cannot create checker of type {type.FullName}"));
        return checker.CanBeRegistered(instanceConfig);
    }

    public static bool ShouldRegisterType(Type type, InstanceConfig instanceConfig) {
        return type.GetCustomAttribute<RegisterIf>()?.CanBeRegistered(instanceConfig) ?? true;
    }

    public interface IRegisterIfChecker {
        public bool CanBeRegistered(InstanceConfig instanceConfig);
    }

    public class LoggingEnabled : IRegisterIfChecker {
        public bool CanBeRegistered(InstanceConfig instanceConfig) {
            return instanceConfig.IsLoggingEnabled();
        }
    }
}