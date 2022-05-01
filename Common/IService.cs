using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common {
    public interface IService {
        public static Task ProcessEventAsync(IEnumerable<IService> services, ServiceEventType serviceEventType) {
            var tasks = services.Select(service => {
                return serviceEventType switch {
                    ServiceEventType.PreDiscordLogin    => service.OnPreDiscordLogin(),
                    ServiceEventType.PreDiscordStart    => service.OnPreDiscordStart(),
                    ServiceEventType.PostDiscordStart   => service.OnPostDiscordStart(),
                    ServiceEventType.DiscordReady       => service.OnDiscordReady(),
                    ServiceEventType.ShutdownNotStarted => service.OnShutdown(false),
                    ServiceEventType.ShutdownStarted    => service.OnShutdown(true),
                    _                                   => throw new ArgumentOutOfRangeException(nameof(serviceEventType), serviceEventType, "No such ServiceEventType")
                };
            });
            return Task.WhenAll(tasks).WhenEnd();
        }

        Task OnPreDiscordLogin() {
            return Task.CompletedTask;
        }

        Task OnPreDiscordStart() {
            return Task.CompletedTask;
        }

        Task OnPostDiscordStart() {
            return Task.CompletedTask;
        }
        
        Task OnDiscordReady() {
            return Task.CompletedTask;
        }

        Task OnShutdown(bool isDiscordStarted) {
            return Task.CompletedTask;
        }
    }

    public enum ServiceEventType {
        PreDiscordLogin,
        PreDiscordStart,
        PostDiscordStart,
        DiscordReady,
        ShutdownNotStarted,
        ShutdownStarted,
    }
}