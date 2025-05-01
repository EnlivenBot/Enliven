using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Common;

public interface IService
{
    public static Task ProcessEventAsync(IEnumerable<IService> services, ServiceEventType serviceEventType,
        ILogger? logger)
    {
        var tasks = services.Select(async service =>
        {
            var actionTask = serviceEventType switch
            {
                ServiceEventType.PreDiscordLogin => service.OnPreDiscordLogin(),
                ServiceEventType.PreDiscordStart => service.OnPreDiscordStart(),
                ServiceEventType.PostDiscordStart => service.OnPostDiscordStart(),
                ServiceEventType.DiscordReady => service.OnDiscordReady(),
                ServiceEventType.ShutdownNotStarted => service.OnShutdown(false),
                ServiceEventType.ShutdownStarted => service.OnShutdown(true),
                _ => throw new ArgumentOutOfRangeException(nameof(serviceEventType), serviceEventType,
                    "No such ServiceEventType")
            };
            try
            {
                await actionTask;
            }
            catch (Exception e)
            {
                logger?.LogError(e, "Exception in {ServiceType} while {EventType}", service, serviceEventType);
            }
        });
        return Task.WhenAll(tasks).WhenEnd();
    }

    Task OnPreDiscordLogin()
    {
        return Task.CompletedTask;
    }

    Task OnPreDiscordStart()
    {
        return Task.CompletedTask;
    }

    Task OnPostDiscordStart()
    {
        return Task.CompletedTask;
    }

    Task OnDiscordReady()
    {
        return Task.CompletedTask;
    }

    Task OnShutdown(bool isDiscordStarted)
    {
        return Task.CompletedTask;
    }
}

public enum ServiceEventType
{
    PreDiscordLogin,
    PreDiscordStart,
    PostDiscordStart,
    DiscordReady,
    ShutdownNotStarted,
    ShutdownStarted,
}