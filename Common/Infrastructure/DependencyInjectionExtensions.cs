using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Infrastructure;

public static class DependencyInjectionExtensions {
    public static IServiceCollection ConfigureNamedOptions<T>(this IServiceCollection services,
        IConfiguration configuration) where T : class {
        return services.Configure<T>(configuration.GetSection(typeof(T).Name));
    }
}