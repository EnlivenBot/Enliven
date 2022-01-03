using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;

namespace Bot.Utilities.Logging {
    public class BotInstanceNlogModule : Module {
        private readonly IResolveMiddleware _middleware;

        public BotInstanceNlogModule() {
            _middleware = new BotInstanceNlogMiddleware();
        }

        protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry, IComponentRegistration registration) {
            // Attach to the registration's pipeline build.
            registration.PipelineBuilding += (sender, pipeline) => {
                // Add our middleware to the pipeline.
                pipeline.Use(_middleware);
            };
        }
    }
}