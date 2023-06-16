using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Autofac.Core.Resolving.Pipeline;
using Common.Config;
using NLog;

namespace Bot.Utilities.Logging {
    public class BotInstanceNlogMiddleware : IResolveMiddleware {
        public PipelinePhase Phase => PipelinePhase.ParameterSelection;

        public void Execute(ResolveRequestContext context, System.Action<ResolveRequestContext> next) {
            // Add our parameters.
            context.ChangeParameters(context.Parameters.Union(
                new[] {
                    new ResolvedParameter(
                        (p, i) => p.ParameterType == typeof(ILogger),
                        (p, i) => {
                            var logger = LogManager.GetLogger(p.Member.DeclaringType.FullName);
                            if (i.TryResolve<InstanceConfig>(out var configProvider)) logger = logger.WithProperty("DisplayedInstanceName", $"{configProvider.Name}|");
                            return logger;
                        })
                }));

            // Continue the resolve.
            next(context);

            // Has an instance been activated?
            if (context.NewInstanceActivated) {
                var instanceType = context.Instance.GetType();

                // Get all the injectable properties to set.
                // If you wanted to ensure the properties were only UNSET properties,
                // here's where you'd do it.
                var properties = instanceType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType == typeof(ILogger) && p.CanWrite && p.GetIndexParameters().Length == 0);

                // Set the properties located.
                foreach (var propToSet in properties) {
                    var logger = LogManager.GetLogger(instanceType.FullName);
                    if (context.TryResolve<InstanceConfig>(out var configProvider)) logger = logger.WithProperty("DisplayedInstanceName", $"{configProvider.Name}|");
                    propToSet.SetValue(context.Instance, logger, null);
                }
            }
        }
    }
}