using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Microsoft.Extensions.Hosting;

namespace Common.Music.Players;

public class PlayerManagerBackgroundService : BackgroundService
{
    private readonly IPlayerManager _playerManager;

    public PlayerManagerBackgroundService(IPlayerManager playerManager)
    {
        _playerManager = playerManager;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _playerManager.PlayerCreated += OnPlayerCreated;
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _playerManager.PlayerCreated -= OnPlayerCreated;
        return Task.CompletedTask;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }

    private async Task OnPlayerCreated(object sender, PlayerCreatedEventArgs args)
    {
        if (args.Player is IPlayerOnReady player)
        {
            await player.OnReady();
        }
    }
}