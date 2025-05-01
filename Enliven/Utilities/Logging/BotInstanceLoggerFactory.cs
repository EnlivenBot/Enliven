using Common.Config;
using Microsoft.Extensions.Logging;

namespace Bot.Utilities.Logging;

public class BotInstanceLoggerFactoryDecorator(ILoggerFactory loggerFactoryImplementation, InstanceConfig config)
    : ILoggerFactory
{
    public void Dispose()
    {
        loggerFactoryImplementation.Dispose();
    }

    public void AddProvider(ILoggerProvider provider)
    {
        loggerFactoryImplementation.AddProvider(provider);
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = loggerFactoryImplementation.CreateLogger(categoryName);
        logger.BeginScope(("InstanceName", config.Name));
        return logger;
    }
}