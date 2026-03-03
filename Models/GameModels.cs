namespace PokerServer.Models;

public class PlayerInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Balance { get; set; } = 1000;
    public int CurrentBet { get; set; }
    public int TotalBetThisRound { get; set; } // Total bet in current betting round
    public List<CardInfo> Hand { get; set; } = [];
    public int SeatPosition { get; set; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Waiting;
    public bool IsReady { get; set; }
    public bool HasActedThisRound { get; set; } // For poker betting rounds
    public bool IsFolded { get; set; }
    public bool IsAllIn { get; set; }
    public string? HandDescription { get; set; } // Description of poker hand
}

public class CardInfo
{
    public string Suit { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsFaceUp { get; set; } = true;

    public int GetNumericValue(int currentTotal = 0)
    {
        return Value switch
        {
            "Ace" => currentTotal + 11 > 21 ? 1 : 11,
            "King" or "Queen" or "Jack" => 10,
            _ => int.TryParse(Value, out int val) ? val : 0
        };
    }
}

public class GameRoom
{
    public string RoomId { get; set; } = Guid.NewGuid().ToString();
    public string RoomName { get; set; } = string.Empty;
    public GameMode Mode { get; set; } = GameMode.Blackjack;
    public GamePhase Phase { get; set; } = GamePhase.WaitingForPlayers;
    public List<PlayerInfo> Players { get; set; } = [];
    public PlayerInfo? Dealer { get; set; }
    public List<CardInfo> DealerHand { get; set; } = [];
    public List<CardInfo> CommunityCards { get; set; } = []; // For Poker
    public List<CardInfo> Deck { get; set; } = [];
    public int CurrentPlayerIndex { get; set; }
    public int MinBet { get; set; } = 10;
    public int MaxPlayers { get; set; } = 6;
    public int Pot { get; set; } // For Poker
    public int CurrentBetToMatch { get; set; } // For Poker
    public PokerRound PokerRound { get; set; } = PokerRound.PreFlop; // Current poker round
    public int DealerButtonIndex { get; set; } // Dealer button position for poker
    public int SmallBlindAmount { get; set; } = 5;
    public int BigBlindAmount { get; set; } = 10;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlayerInfo? CurrentPlayer => 
        CurrentPlayerIndex >= 0 && CurrentPlayerIndex < Players.Count 
            ? Players[CurrentPlayerIndex] 
            : null;

    // Minimum players required to start
    public int MinPlayersRequired => Mode == GameMode.Poker ? 2 : 1;
    
    // Get active players (not folded)
    public List<PlayerInfo> ActivePlayers => Players.Where(p => !p.IsFolded && p.Status == PlayerStatus.Playing).ToList();
}

public class GameStateDto
{
    public string RoomId { get; set; } = string.Empty;
    public GameMode Mode { get; set; }
    public GamePhase Phase { get; set; }
    public PokerRound PokerRound { get; set; } // Current poker betting round
    public List<PlayerDto> Players { get; set; } = [];
    public List<CardInfo> DealerHand { get; set; } = [];
    public List<CardInfo> CommunityCards { get; set; } = []; // For Poker
    public int DealerHandValue { get; set; }
    public int Pot { get; set; } // For Poker
    public int CurrentBetToMatch { get; set; } // For Poker
    public int MinRaise { get; set; } // Minimum raise amount
    public string? CurrentPlayerId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int MinPlayersRequired { get; set; }
    public int CurrentPlayerCount { get; set; }
    public int DealerButtonIndex { get; set; }
}

public class PlayerDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Balance { get; set; }
    public int CurrentBet { get; set; }
    public int TotalBetThisRound { get; set; }
    public List<CardInfo> Hand { get; set; } = [];
    public int HandValue { get; set; }
    public string? HandDescription { get; set; }
    public bool IsFolded { get; set; }
    public bool IsAllIn { get; set; }
    public int SeatPosition { get; set; }
    public PlayerStatus Status { get; set; }
}

public class RoomInfoDto
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public GameMode Mode { get; set; }
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public GamePhase Phase { get; set; }
    public int MinBet { get; set; }
}

public enum GameMode
{
    Blackjack,
    Poker
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

// Poker-specific phases
public enum PokerRound
{
    PreFlop,
    Flop,
    Turn,
    River,
    Showdown
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
