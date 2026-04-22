using Microsoft.AspNetCore.SignalR;
using PokerServer.Hubs;

namespace PokerServer.Services;

/// <summary>
/// Background service that listens for turn timeout events from GameService
/// and broadcasts them via SignalR using IHubContext (thread-safe, no leak).
/// </summary>
public class TurnTimeoutService : IHostedService, IDisposable
{
    private readonly GameService _gameService;
    private readonly IHubContext<GameHub> _hubContext;

    public TurnTimeoutService(GameService gameService, IHubContext<GameHub> hubContext)
    {
        _gameService = gameService;
        _hubContext = hubContext;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _gameService.OnTurnTimeout += HandleTurnTimeout;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _gameService.OnTurnTimeout -= HandleTurnTimeout;
        return Task.CompletedTask;
    }

    private async void HandleTurnTimeout(string roomId, string connectionId)
    {
        try
        {
            var group = _hubContext.Clients.Group(roomId);
            await group.SendAsync("TurnTimeout", connectionId);
            await group.SendAsync("PlayerFolded", connectionId);
            
            // Send per-player game states for proper card hiding
            var room = _gameService.GetRoom(roomId);
            if (room != null)
            {
                foreach (var player in room.Players)
                {
                    var state = _gameService.GetGameState(roomId, player.ConnectionId);
                    await _hubContext.Clients.Client(player.ConnectionId).SendAsync("GameStateUpdated", state);
                }
                var spectatorState = _gameService.GetGameState(roomId);
                foreach (var spectator in room.Spectators)
                {
                    await _hubContext.Clients.Client(spectator.ConnectionId).SendAsync("GameStateUpdated", spectatorState);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling turn timeout: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _gameService.OnTurnTimeout -= HandleTurnTimeout;
    }
}
