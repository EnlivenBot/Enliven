﻿using System;
using System.Threading.Tasks;
using Bot.DiscordRelated;
using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Configuration;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Bot.Utilities.Logging;

public static class LoggingUtilities {
    public static Task OnDiscordLog(ILogger logger, LogMessage message) {
        if (message.Exception is CommandInterruptionException)
            return Task.CompletedTask;
        if (message.Message != null && message.Message.StartsWith("Unknown Dispatch"))
            return Task.CompletedTask;

        logger.LogDiscord(message.Severity, message.Exception, "{message} from {source}", message.Message!, message.Source);
        return Task.CompletedTask;
    }

    public static void LogDiscord(this ILogger logger, LogSeverity logSeverity, Exception exception, string message, params object[] args) {
        logger.Log(logSeverity.ToLogLevel(), exception, message, args);
    }

    public static LogLevel ToLogLevel(this LogSeverity logSeverity) {
        var logLevel = logSeverity switch {
            LogSeverity.Critical => LogLevel.Error,
            LogSeverity.Error    => LogLevel.Error,
            LogSeverity.Warning  => LogLevel.Warning,
            LogSeverity.Info     => LogLevel.Information,
            LogSeverity.Verbose  => LogLevel.Debug,
            LogSeverity.Debug    => LogLevel.Trace,
            _                    => throw new ArgumentOutOfRangeException()
        };
        return logLevel;
    }
    
    public static LoggerConfiguration CustomLogLevelFromConfiguration(
        this LoggerSettingsConfiguration settingConfiguration,
        IConfiguration configuration)
    {
        return settingConfiguration.Settings(new SerilogFilterConfigurationReader(configuration));
    }
}