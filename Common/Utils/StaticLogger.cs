using System;
using Microsoft.Extensions.Logging;

namespace Common.Utils;

/// <summary>
/// Temporary hack to create loggers for static classes
/// </summary>
/// Probably should be removed later
public static class StaticLogger
{
    private static ILoggerFactory? _loggerFactory;

    public static void Setup(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public static ILogger<T> Create<T>()
    {
        return new Logger<T>(_loggerFactory ?? throw new InvalidOperationException("Logger factory isn't initialized"));
    }

    public static ILogger Create(string className)
    {
        return _loggerFactory?.CreateLogger(className) ??
               throw new InvalidOperationException("Logger factory isn't initialized");
    }
}