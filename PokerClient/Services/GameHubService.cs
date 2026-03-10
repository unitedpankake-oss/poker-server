using Microsoft.AspNetCore.SignalR.Client;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlackjackGame.Services;

public class GameHubService : INotifyPropertyChanged, IAsyncDisposable
{
    private HubConnection? _connection;
    private string _connectionId = string.Empty;
    private bool _isConnected;

    public string ConnectionId => _connectionId;
    public bool IsConnected => _isConnected;

    public event Action<string>? OnConnected;
    public event Action<string>? OnDisconnected;
    public event Action<PlayerInfo>? OnPlayerJoined;
    public event Action<string>? OnPlayerLeft;
    public event Action<GameStateDto>? OnGameStateUpdated;
    public event Action? OnGameStarted;
    public event Action<string, CardDto>? OnCardDealt;
    public event Action? OnDealerTurnComplete;
    public event Action? OnNewRoundStarted;
    public event Action<string, string>? OnChatMessageReceived;
    public event Action<string, int>? OnBetPlaced;
    public event Action<string>? OnPlayerReady;
    // New events for spectator/timer/muck features
    public event Action<string, int>? OnPlayerSeated;
    public event Action<string>? OnPlayerStoodUp;
    public event Action<string>? OnTurnTimeout;
    public event Action<string, int>? OnTurnTimerStarted;
    public event Action<string>? OnPlayerMucked;
    public event Action<string>? OnPlayerShowedCards;
    public event Action<string>? OnRoomDeleted;
    public event Action? OnRoomClosed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task ConnectAsync(string serverUrl)
    {
        if (_connection != null)
            await DisconnectAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .Build();

        SetupEventHandlers();

        await _connection.StartAsync();
    }

    private void SetupEventHandlers()
    {
        if (_connection == null) return;

        _connection.On<string>("Connected", (connectionId) =>
        {
            _connectionId = connectionId;
            _isConnected = true;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ConnectionId));
            OnConnected?.Invoke(connectionId);
        });

        _connection.On<PlayerInfo>("PlayerJoined", (player) =>
        {
            OnPlayerJoined?.Invoke(player);
        });

        _connection.On<string>("PlayerLeft", (connectionId) =>
        {
            OnPlayerLeft?.Invoke(connectionId);
        });

        _connection.On<GameStateDto>("GameStateUpdated", (state) =>
        {
            OnGameStateUpdated?.Invoke(state);
        });

        _connection.On("GameStarted", () =>
        {
            OnGameStarted?.Invoke();
        });

        _connection.On<string, CardDto>("CardDealt", (playerId, card) =>
        {
            OnCardDealt?.Invoke(playerId, card);
        });

        _connection.On("DealerTurnComplete", () =>
        {
            OnDealerTurnComplete?.Invoke();
        });

        _connection.On("NewRoundStarted", () =>
        {
            OnNewRoundStarted?.Invoke();
        });

        _connection.On<string, string>("ChatMessageReceived", (username, message) =>
        {
            OnChatMessageReceived?.Invoke(username, message);
        });

        _connection.On<string, int>("BetPlaced", (playerId, amount) =>
        {
            OnBetPlaced?.Invoke(playerId, amount);
        });

        _connection.On<string>("PlayerReady", (playerId) =>
        {
            OnPlayerReady?.Invoke(playerId);
        });

        _connection.On<string, int>("PlayerSeated", (playerId, seatPosition) =>
        {
            OnPlayerSeated?.Invoke(playerId, seatPosition);
        });

        _connection.On<string>("PlayerStoodUp", (playerId) =>
        {
            OnPlayerStoodUp?.Invoke(playerId);
        });

        _connection.On<string>("TurnTimeout", (playerId) =>
        {
            OnTurnTimeout?.Invoke(playerId);
        });

        _connection.On<string, int>("TurnTimerStarted", (playerId, seconds) =>
        {
            OnTurnTimerStarted?.Invoke(playerId, seconds);
        });

        _connection.On<string>("PlayerMucked", (playerId) =>
        {
            OnPlayerMucked?.Invoke(playerId);
        });

        _connection.On<string>("PlayerShowedCards", (playerId) =>
        {
            OnPlayerShowedCards?.Invoke(playerId);
        });

        _connection.On<string>("RoomDeleted", (roomId) =>
        {
            OnRoomDeleted?.Invoke(roomId);
        });

        _connection.On("RoomClosed", () =>
        {
            OnRoomClosed?.Invoke();
        });

        _connection.Closed += async (error) =>
        {
            _isConnected = false;
            OnPropertyChanged(nameof(IsConnected));
            OnDisconnected?.Invoke(error?.Message ?? "Connection closed");
            await Task.CompletedTask;
        };

        _connection.Reconnected += async (connectionId) =>
        {
            _connectionId = connectionId ?? string.Empty;
            _isConnected = true;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ConnectionId));
            OnConnected?.Invoke(_connectionId);
            await Task.CompletedTask;
        };
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
            _isConnected = false;
            OnPropertyChanged(nameof(IsConnected));
        }
    }

    public async Task<List<RoomInfoDto>> GetRoomsAsync()
    {
        if (_connection == null) return [];
        return await _connection.InvokeAsync<List<RoomInfoDto>>("GetRooms");
    }

    public async Task<RoomInfoDto?> CreateRoomAsync(string roomName, int mode, int minBet)
    {
        if (_connection == null) return null;
        return await _connection.InvokeAsync<RoomInfoDto?>("CreateRoom", roomName, mode, minBet, false);
    }

    public async Task<bool> JoinRoomAsync(string roomId, string username, int balance, bool asSpectator = false)
    {
        if (_connection == null) return false;
        return await _connection.InvokeAsync<bool>("JoinRoom", roomId, username, balance, asSpectator);
    }

    public async Task<List<int>> GetAvailableSeatsAsync(string roomId)
    {
        if (_connection == null) return [];
        return await _connection.InvokeAsync<List<int>>("GetAvailableSeats", roomId);
    }

    public async Task<bool> TakeSeatAsync(string roomId, int seatPosition)
    {
        if (_connection == null) return false;
        return await _connection.InvokeAsync<bool>("TakeSeat", roomId, seatPosition);
    }

    public async Task<bool> StandUpAsync(string roomId)
    {
        if (_connection == null) return false;
        return await _connection.InvokeAsync<bool>("StandUp", roomId);
    }

    public async Task LeaveRoomAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("LeaveRoom", roomId);
    }

    public async Task PlaceBetAsync(string roomId, int amount)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("PlaceBet", roomId, amount);
    }

    // For Poker: just mark as ready (blinds are automatic)
    public async Task SetReadyAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("SetReady", roomId);
    }

    public async Task HitAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("Hit", roomId);
    }

    public async Task StandAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("Stand", roomId);
    }

    // Poker actions
    public async Task PokerFoldAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("PokerFold", roomId);
    }

    public async Task PokerCheckAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("PokerCheck", roomId);
    }

    public async Task PokerCallAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("PokerCall", roomId);
    }

    public async Task PokerRaiseAsync(string roomId, int amount)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("PokerRaise", roomId, amount);
    }

    public async Task PokerAllInAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("PokerAllIn", roomId);
    }

    public async Task MuckCardsAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("MuckCards", roomId);
    }

    public async Task ShowCardsAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("ShowCards", roomId);
    }

    public async Task<int> GetRemainingTimeAsync(string roomId)
    {
        if (_connection == null) return 0;
        return await _connection.InvokeAsync<int>("GetRemainingTime", roomId);
    }

    public async Task NewRoundAsync(string roomId)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("NewRound", roomId);
    }

    public async Task SendChatMessageAsync(string roomId, string message)
    {
        if (_connection == null) return;
        await _connection.InvokeAsync("SendChatMessage", roomId, message);
    }

    public async Task<bool> DeleteRoomAsync(string roomId)
    {
        if (_connection == null) return false;
        return await _connection.InvokeAsync<bool>("DeleteRoom", roomId);
    }

    public async Task<bool> ForceDeleteRoomAsync(string roomId)
    {
        if (_connection == null) return false;
        return await _connection.InvokeAsync<bool>("ForceDeleteRoom", roomId);
    }

    // Authentication methods
    public async Task<UserDto?> LoginAsync(string username, string password)
    {
        if (_connection == null) return null;
        return await _connection.InvokeAsync<UserDto?>("Login", username, password);
    }

    public async Task<(bool Success, string Message, UserDto? User)> RegisterAsync(string username, string email, string password)
    {
        if (_connection == null) return (false, "Not connected", null);
        return await _connection.InvokeAsync<(bool, string, UserDto?)>("Register", username, email, password);
    }

    public async Task<bool> UpdateUserBalanceAsync(string userId, int newBalance)
    {
        if (_connection == null) return false;
        return await _connection.InvokeAsync<bool>("UpdateUserBalance", userId, newBalance);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

// DTO classes matching server models
public class RoomInfoDto
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public int Mode { get; set; }
    public int PlayerCount { get; set; }
    public int SpectatorCount { get; set; }
    public int MaxPlayers { get; set; }
    public int Phase { get; set; }
    public int MinBet { get; set; }
    public int AvailableSeats { get; set; }
    public bool IsPersistent { get; set; }
    
    public string ModeName => Mode == 0 ? "Blackjack" : "Poker";
    public int MinPlayersRequired => Mode == 1 ? 2 : 1; // Poker needs 2, Blackjack needs 1
}

public class GameStateDto
{
    public string RoomId { get; set; } = string.Empty;
    public int Mode { get; set; }
    public int Phase { get; set; }
    public int PokerRound { get; set; } // 0=PreFlop, 1=Flop, 2=Turn, 3=River, 4=Showdown
    public List<PlayerInfo> Players { get; set; } = [];
    public List<PlayerInfo> Spectators { get; set; } = [];
    public List<CardDto> DealerHand { get; set; } = [];
    public List<CardDto> CommunityCards { get; set; } = []; // For Poker
    public int DealerHandValue { get; set; }
    public int Pot { get; set; } // For Poker
    public int CurrentBetToMatch { get; set; } // For Poker
    public int MinRaise { get; set; } // Minimum raise amount
    public string? CurrentPlayerId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int MinPlayersRequired { get; set; }
    public int CurrentPlayerCount { get; set; }
    public int DealerButtonIndex { get; set; }
    public int TurnTimeoutSeconds { get; set; } = 30;
    public DateTime? TurnStartTime { get; set; }
    public bool IsShowdownPending { get; set; }
    
    public bool IsPoker => Mode == 1;
    public string PokerRoundName => PokerRound switch
    {
        0 => "Pre-Flop",
        1 => "Flop",
        2 => "Turn",
        3 => "River",
        4 => "Showdown",
        _ => ""
    };
}

public class PlayerInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Balance { get; set; }
    public int CurrentBet { get; set; }
    public int TotalBetThisRound { get; set; }
    public List<CardDto> Hand { get; set; } = [];
    public int HandValue { get; set; }
    public string? HandDescription { get; set; }
    public int SeatPosition { get; set; }
    public int Status { get; set; }
    public bool IsFolded { get; set; }
    public bool IsAllIn { get; set; }
    public bool IsSpectator { get; set; }
    public bool ShowCards { get; set; } = true;
}

public class CardDto
{
    public string Suit { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsFaceUp { get; set; } = true;
}

public enum GamePhase
{
    WaitingForPlayers,
    Betting,
    Dealing,
    PlayerTurn,
    DealerTurn,
    Showdown,
    GameOver
}

public enum PlayerStatus
{
    Waiting,
    Betting,
    Playing,
    Standing,
    Busted,
    Blackjack,
    Won,
    Lost,
    Push,
    Disconnected
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Balance { get; set; }
    public string AvatarUrl { get; set; } = "default";
    public DateTime CreatedAt { get; set; }
    public DateTime LastLogin { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int TotalWinnings { get; set; }
}
