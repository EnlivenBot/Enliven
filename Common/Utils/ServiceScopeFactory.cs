using System;
using Autofac;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Utils {
    public class ServiceScopeFactoryAdapter : IServiceScopeFactory {
        private readonly ILifetimeScope _lifetimeScope;
        public ServiceScopeFactoryAdapter(ILifetimeScope lifetimeScope) {
            _lifetimeScope = lifetimeScope;
        }
        public IServiceScope CreateScope() {
            return new ServiceScopeAdapter(_lifetimeScope.BeginLifetimeScope());
        }

        private class ServiceScopeAdapter : IServiceScope {
            private readonly ILifetimeScope _lifetimeScope;
            private readonly ServiceProviderAdapter _serviceProviderAdapter;
            public ServiceScopeAdapter(ILifetimeScope lifetimeScope) {
                _lifetimeScope = lifetimeScope;
                _serviceProviderAdapter = new ServiceProviderAdapter(_lifetimeScope);
            }

            public void Dispose() {
                _lifetimeScope.Dispose();
            }
            public IServiceProvider ServiceProvider => _serviceProviderAdapter;
        }
    }
}