using Microsoft.AspNetCore.SignalR;
using PokerServer.Models;
using PokerServer.Services;

namespace PokerServer.Hubs;

public class GameHub : Hub
{
    private readonly GameService _gameService;
    private readonly UserService _userService;

    public GameHub(GameService gameService, UserService userService)
    {
        _gameService = gameService;
        _userService = userService;
        _gameService.OnTurnTimeout += HandleTurnTimeout;
    }

    private async void HandleTurnTimeout(string roomId, string connectionId)
    {
        await Clients.Group(roomId).SendAsync("TurnTimeout", connectionId);
        await Clients.Group(roomId).SendAsync("PlayerFolded", connectionId);
        await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
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

    public async Task<RoomInfoDto?> CreateRoom(string roomName, int mode, int minBet, bool isPersistent = true)
    {
        try
        {
            Console.WriteLine($"CreateRoom called: roomName={roomName}, mode={mode}, minBet={minBet}");
            var gameMode = (GameMode)mode;
            var room = _gameService.CreateRoom(roomName, gameMode, minBet, isPersistent);
            room.CreatorId = Context.ConnectionId;
            Console.WriteLine($"Room created: {room.RoomId}");
            return new RoomInfoDto
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                Mode = room.Mode,
                PlayerCount = 0,
                SpectatorCount = 0,
                MaxPlayers = room.MaxPlayers,
                Phase = room.Phase,
                MinBet = room.MinBet,
                AvailableSeats = room.MaxPlayers,
                IsPersistent = room.IsPersistent
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CreateRoom error: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    public async Task<bool> DeleteRoom(string roomId)
    {
        var success = _gameService.DeleteRoom(roomId);
        if (success)
        {
            await Clients.All.SendAsync("RoomDeleted", roomId);
        }
        return success;
    }

    public async Task<bool> ForceDeleteRoom(string roomId)
    {
        var success = _gameService.ForceDeleteRoom(roomId);
        if (success)
        {
            await Clients.Group(roomId).SendAsync("RoomClosed");
            await Clients.All.SendAsync("RoomDeleted", roomId);
        }
        return success;
    }

    public async Task<bool> JoinRoom(string roomId, string username, int balance, bool asSpectator = false)
    {
        var player = _gameService.JoinRoom(roomId, Context.ConnectionId, username, balance, asSpectator);
        if (player == null)
            return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync("PlayerJoined", player);
        
        // Check if we have enough players to start betting
        if (_gameService.CanStartGame(roomId))
        {
            _gameService.TransitionToBetting(roomId);
        }
        
        await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId, Context.ConnectionId));
        return true;
    }

    public async Task<List<int>> GetAvailableSeats(string roomId)
    {
        return _gameService.GetAvailableSeats(roomId);
    }

    public async Task<bool> TakeSeat(string roomId, int seatPosition)
    {
        if (_gameService.TakeSeat(roomId, Context.ConnectionId, seatPosition))
        {
            await Clients.Group(roomId).SendAsync("PlayerSeated", Context.ConnectionId, seatPosition);
            
            if (_gameService.CanStartGame(roomId))
            {
                _gameService.TransitionToBetting(roomId);
            }
            
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
            return true;
        }
        return false;
    }

    public async Task<bool> StandUp(string roomId)
    {
        if (_gameService.StandUp(roomId, Context.ConnectionId))
        {
            await Clients.Group(roomId).SendAsync("PlayerStoodUp", Context.ConnectionId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
            return true;
        }
        return false;
    }

    public async Task LeaveRoom(string roomId)
    {
        _gameService.LeaveRoom(roomId, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync("PlayerLeft", Context.ConnectionId);
        await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
    }

    public async Task<int> GetRemainingTime(string roomId)
    {
        return _gameService.GetRemainingTurnTime(roomId);
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
                
                var state = _gameService.GetGameState(roomId);
                
                // For Blackjack: if all players have Blackjack or Busted, go to dealer turn
                if (state.Mode == GameMode.Blackjack && state.Phase == GamePhase.DealerTurn)
                {
                    await PlayDealerTurn(roomId);
                    return;
                }
                
                // Start turn timer for poker
                if (state.Mode == GameMode.Poker && state.Phase == GamePhase.PlayerTurn)
                {
                    _gameService.StartTurnTimer(roomId);
                    await Clients.Group(roomId).SendAsync("TurnTimerStarted", state.CurrentPlayerId, state.TurnTimeoutSeconds);
                }
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
            _gameService.StopTurnTimer(roomId);
            await Clients.Group(roomId).SendAsync("PlayerFolded", Context.ConnectionId);
            
            var state = _gameService.GetGameState(roomId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", state);
            
            // Start timer for next player if game continues
            if (state.Phase == GamePhase.PlayerTurn)
            {
                _gameService.StartTurnTimer(roomId);
                await Clients.Group(roomId).SendAsync("TurnTimerStarted", state.CurrentPlayerId, state.TurnTimeoutSeconds);
            }
        }
    }

    public async Task PokerCheck(string roomId)
    {
        if (_gameService.PokerCheck(roomId, Context.ConnectionId))
        {
            _gameService.StopTurnTimer(roomId);
            await Clients.Group(roomId).SendAsync("PlayerChecked", Context.ConnectionId);
            
            var state = _gameService.GetGameState(roomId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", state);
            
            if (state.Phase == GamePhase.PlayerTurn)
            {
                _gameService.StartTurnTimer(roomId);
                await Clients.Group(roomId).SendAsync("TurnTimerStarted", state.CurrentPlayerId, state.TurnTimeoutSeconds);
            }
        }
    }

    public async Task PokerCall(string roomId)
    {
        if (_gameService.PokerCall(roomId, Context.ConnectionId))
        {
            _gameService.StopTurnTimer(roomId);
            await Clients.Group(roomId).SendAsync("PlayerCalled", Context.ConnectionId);
            
            var state = _gameService.GetGameState(roomId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", state);
            
            if (state.Phase == GamePhase.PlayerTurn)
            {
                _gameService.StartTurnTimer(roomId);
                await Clients.Group(roomId).SendAsync("TurnTimerStarted", state.CurrentPlayerId, state.TurnTimeoutSeconds);
            }
        }
    }

    public async Task PokerRaise(string roomId, int amount)
    {
        if (_gameService.PokerRaise(roomId, Context.ConnectionId, amount))
        {
            _gameService.StopTurnTimer(roomId);
            await Clients.Group(roomId).SendAsync("PlayerRaised", Context.ConnectionId, amount);
            
            var state = _gameService.GetGameState(roomId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", state);
            
            if (state.Phase == GamePhase.PlayerTurn)
            {
                _gameService.StartTurnTimer(roomId);
                await Clients.Group(roomId).SendAsync("TurnTimerStarted", state.CurrentPlayerId, state.TurnTimeoutSeconds);
            }
        }
    }

    public async Task PokerAllIn(string roomId)
    {
        if (_gameService.PokerAllIn(roomId, Context.ConnectionId))
        {
            _gameService.StopTurnTimer(roomId);
            await Clients.Group(roomId).SendAsync("PlayerAllIn", Context.ConnectionId);
            
            var state = _gameService.GetGameState(roomId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", state);
            
            if (state.Phase == GamePhase.PlayerTurn)
            {
                _gameService.StartTurnTimer(roomId);
                await Clients.Group(roomId).SendAsync("TurnTimerStarted", state.CurrentPlayerId, state.TurnTimeoutSeconds);
            }
        }
    }

    public async Task MuckCards(string roomId)
    {
        if (_gameService.SetShowCards(roomId, Context.ConnectionId, false))
        {
            await Clients.Group(roomId).SendAsync("PlayerMucked", Context.ConnectionId);
            await Clients.Group(roomId).SendAsync("GameStateUpdated", _gameService.GetGameState(roomId));
        }
    }

    public async Task ShowCards(string roomId)
    {
        if (_gameService.SetShowCards(roomId, Context.ConnectionId, true))
        {
            await Clients.Group(roomId).SendAsync("PlayerShowedCards", Context.ConnectionId);
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

    #region User Authentication

    public async Task<UserDto?> Login(string username, string password)
    {
        var user = _userService.Authenticate(username, password);
        if (user == null)
            return null;
        return UserDto.FromAccount(user);
    }

    public async Task<(bool Success, string Message, UserDto? User)> Register(string username, string email, string password)
    {
        var result = _userService.Register(username, email, password);
        if (result.Success && result.User != null)
        {
            return (true, result.Message, UserDto.FromAccount(result.User));
        }
        return (result.Success, result.Message, null);
    }

    public async Task<UserDto?> GetUserInfo(string userId)
    {
        var user = _userService.GetUser(userId);
        return user != null ? UserDto.FromAccount(user) : null;
    }

    public async Task<bool> UpdateUserBalance(string userId, int newBalance)
    {
        return _userService.UpdateBalance(userId, newBalance);
    }

    #endregion

    #region Admin API

    public async Task<List<UserDto>> AdminGetAllUsers(string adminPassword)
    {
        Console.WriteLine($"AdminGetAllUsers called with password: {adminPassword?.Substring(0, Math.Min(3, adminPassword?.Length ?? 0))}***");
        var users = _userService.GetAllUsers(adminPassword);
        Console.WriteLine($"AdminGetAllUsers returning {users.Count} users");
        return users.Select(UserDto.FromAccount).ToList();
    }

    public async Task<bool> AdminUpdateUser(string adminPassword, UserDto user)
    {
        var account = new UserAccount
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Balance = user.Balance,
            GamesPlayed = user.GamesPlayed,
            GamesWon = user.GamesWon,
            TotalWinnings = user.TotalWinnings,
            AvatarUrl = user.AvatarUrl
        };
        return _userService.UpdateUser(adminPassword, account);
    }

    public async Task<bool> AdminSetPassword(string adminPassword, string userId, string newPassword)
    {
        return _userService.SetPassword(adminPassword, userId, newPassword);
    }

    public async Task<bool> AdminDeleteUser(string adminPassword, string userId)
    {
        return _userService.DeleteUser(adminPassword, userId);
    }

    public async Task<bool> AdminAddBalance(string adminPassword, string userId, int amount)
    {
        if (!_userService.ValidateAdminPassword(adminPassword))
            return false;
        return _userService.AddBalance(userId, amount);
    }

    #endregion
}
