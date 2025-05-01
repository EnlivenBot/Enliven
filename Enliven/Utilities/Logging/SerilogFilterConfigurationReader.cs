using System;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Bot.Utilities.Logging;

public class SerilogFilterConfigurationReader(IConfiguration configuration) : ILoggerSettings
{
    public void Configure(LoggerConfiguration loggerConfiguration)
    {
        foreach (var section in configuration.GetSection("Serilog").GetSection("CustomLogLevel").GetChildren())
        {
            var level = Enum.Parse<LogEventLevel>(section.Value!, true);
            loggerConfiguration.Filter.ByExcluding(e =>
            {
                if (!e.Properties.TryGetValue("SourceContext", out var sourceContextValue) || sourceContextValue is not ScalarValue scalarValue)
                {
                    return false;
                }

                var nameSpace = (string)scalarValue.Value!;
                return e.Level < level && nameSpace.StartsWith(section.Key);
            });
        }
    }
}