using System;
using Autofac;

namespace Common.Utils {
    public class ServiceProviderAdapter : IServiceProvider {
        private IComponentContext _context;
        public ServiceProviderAdapter(IComponentContext context) {
            _context = context;
        }

        public object GetService(Type serviceType) {
            return _context.Resolve(serviceType);
        }
    }
}