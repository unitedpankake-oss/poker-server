using Microsoft.AspNetCore.SignalR;
using PokerServer.Models;
using PokerServer.Services;

namespace PokerServer.Hubs;

public class GameHub : Hub
{
    private readonly GameService _gameService;

    public GameHub(GameService gameService)
    {
        _gameService = gameService;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var roomId = _gameService.FindRoomByConnection(Context.ConnectionId);
        if (roomId != null)
        {
            _gameService.LeaveRoom(roomId, Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).SendAsync("PlayerLeft", Context.ConnectionId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<List<RoomInfoDto>> GetRooms()
    {
        return _gameService.GetAvailableRooms();
    }

    public async Task<RoomInfoDto?> CreateRoom(string roomName, GameMode mode, int minBet)
    {
        var room = _gameService.CreateRoom(roomName, mode, minBet);
        return new RoomInfoDto
        {
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            Mode = room.Mode,
            PlayerCount = 0,
            MaxPlayers = room.MaxPlayers,
            Phase = room.Phase,
            MinBet = room.MinBet
        };
    }

    public async Task<bool> JoinRoom(string roomId, string username, int balance)
    {
        var player = _gameService.JoinRoom(roomId, Context.ConnectionId, username, balance);
        if (player == null)
            return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync("PlayerJoined", player);
        
        // Check if we have enough players to start betting
        if (_gameService.CanStartGame(roomId))
        {
            _gameService.TransitionToBetting(roomId);
        }
        
        await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        return true;
    }

    public async Task LeaveRoom(string roomId)
    {
        _gameService.LeaveRoom(roomId, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync("PlayerLeft", Context.ConnectionId);
        await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
    }

    public async Task PlaceBet(string roomId, int amount)
    {
        if (_gameService.PlaceBet(roomId, Context.ConnectionId, amount))
        {
            await Clients.Group(roomId).SendAsync("BetPlaced", Context.ConnectionId, amount);
            
            if (_gameService.AllPlayersReady(roomId))
            {
                _gameService.StartGame(roomId);
                await Clients.Group(roomId).SendAsync("GameStarted");
            }
            
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        }
    }

    public async Task Hit(string roomId)
    {
        var (success, card) = _gameService.Hit(roomId, Context.ConnectionId);
        if (success)
        {
            await Clients.Group(roomId).SendAsync("CardDealt", Context.ConnectionId, card);
            
            var state = _gameService.GetGameState(roomId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", state);
            
            
            if (state.Phase == GamePhase.DealerTurn)
            {
                await PlayDealerTurn(roomId);
            }
        }
    }

    public async Task Stand(string roomId)
    {
        if (_gameService.Stand(roomId, Context.ConnectionId))
        {
            var state = _gameService.GetGameState(roomId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", state);
            
            if (state.Phase == GamePhase.DealerTurn)
            {
                await PlayDealerTurn(roomId);
            }
        }
    }

    #region Poker Actions

    public async Task PokerFold(string roomId)
    {
        if (_gameService.PokerFold(roomId, Context.ConnectionId))
        {
            await Clients.Group(roomId).SendAsync("PlayerFolded", Context.ConnectionId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        }
    }

    public async Task PokerCheck(string roomId)
    {
        if (_gameService.PokerCheck(roomId, Context.ConnectionId))
        {
            await Clients.Group(roomId).SendAsync("PlayerChecked", Context.ConnectionId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        }
    }

    public async Task PokerCall(string roomId)
    {
        if (_gameService.PokerCall(roomId, Context.ConnectionId))
        {
            await Clients.Group(roomId).SendAsync("PlayerCalled", Context.ConnectionId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        }
    }

    public async Task PokerRaise(string roomId, int amount)
    {
        if (_gameService.PokerRaise(roomId, Context.ConnectionId, amount))
        {
            await Clients.Group(roomId).SendAsync("PlayerRaised", Context.ConnectionId, amount);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        }
    }

    public async Task PokerAllIn(string roomId)
    {
        if (_gameService.PokerAllIn(roomId, Context.ConnectionId))
        {
            await Clients.Group(roomId).SendAsync("PlayerAllIn", Context.ConnectionId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        }
    }

    #endregion

    private async Task PlayDealerTurn(string roomId)
    {
        await Task.Delay(500); // Small delay for dramatic effect
        _gameService.PlayDealerTurn(roomId);
        await Clients.Group(roomId).SendAsync("DealerTurnComplete");
        await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
    }

    public async Task NewRound(string roomId)
    {
        var room = _gameService.GetRoom(roomId);
        if (room?.Mode == GameMode.Poker)
        {
            _gameService.ResetPokerForNewRound(roomId);
        }
        else
        {
            _gameService.ResetForNewRound(roomId);
        }
        await Clients.Group(roomId).SendAsync("NewRoundStarted");
        await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
    }

    public async Task SendChatMessage(string roomId, string message)
    {
        var room = _gameService.GetRoom(roomId);
        var player = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (player != null)
        {
            await Clients.Group(roomId).SendAsync("ChatMessageReceived", player.Username, message);
        }
    }
}
