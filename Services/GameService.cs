using PokerServer.Models;

namespace PokerServer.Services;

public class GameService
{
    private readonly Dictionary<string, GameRoom> _rooms = new();
    private readonly object _lock = new();
    private static readonly string[] Suits = ["Hearts", "Diamonds", "Clubs", "Spades"];
    private static readonly string[] Values = ["Ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King"];

    public GameRoom CreateRoom(string roomName, GameMode mode, int minBet = 10)
    {
        var room = new GameRoom
        {
            RoomName = roomName,
            Mode = mode,
            MinBet = minBet,
            Deck = CreateDeck()
        };

        lock (_lock)
        {
            _rooms[room.RoomId] = room;
        }

        return room;
    }

    public List<RoomInfoDto> GetAvailableRooms()
    {
        lock (_lock)
        {
            return _rooms.Values
                .Where(r => r.Players.Count < r.MaxPlayers)
                .Select(r => new RoomInfoDto
                {
                    RoomId = r.RoomId,
                    RoomName = r.RoomName,
                    Mode = r.Mode,
                    PlayerCount = r.Players.Count,
                    MaxPlayers = r.MaxPlayers,
                    Phase = r.Phase,
                    MinBet = r.MinBet
                })
                .ToList();
        }
    }

    public GameRoom? GetRoom(string roomId)
    {
        lock (_lock)
        {
            return _rooms.TryGetValue(roomId, out var room) ? room : null;
        }
    }

    public PlayerInfo? JoinRoom(string roomId, string connectionId, string username, int balance)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return null;

            if (room.Players.Count >= room.MaxPlayers)
                return null;

            var existingPlayer = room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (existingPlayer != null)
                return existingPlayer;

            var seatPosition = Enumerable.Range(0, room.MaxPlayers)
                .First(i => room.Players.All(p => p.SeatPosition != i));

            var player = new PlayerInfo
            {
                ConnectionId = connectionId,
                Username = username,
                Balance = balance,
                SeatPosition = seatPosition,
                Status = PlayerStatus.Waiting
            };

            room.Players.Add(player);
            return player;
        }
    }

    public void LeaveRoom(string roomId, string connectionId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return;

            room.Players.RemoveAll(p => p.ConnectionId == connectionId);

            if (room.Players.Count == 0)
            {
                _rooms.Remove(roomId);
            }
        }
    }

    public bool PlaceBet(string roomId, string connectionId, int amount)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return false;

            var player = room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player == null || amount > player.Balance || amount < room.MinBet)
                return false;

            player.CurrentBet = amount;
            player.Balance -= amount;
            player.Status = PlayerStatus.Betting;
            player.IsReady = true;

            return true;
        }
    }

    public bool AllPlayersReady(string roomId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return false;

            // Check minimum players for game mode
            if (room.Players.Count < room.MinPlayersRequired)
                return false;

            return room.Players.All(p => p.IsReady);
        }
    }

    public bool CanStartGame(string roomId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return false;

            return room.Players.Count >= room.MinPlayersRequired;
        }
    }

    public void TransitionToBetting(string roomId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return;

            // Only transition if currently waiting for players
            if (room.Phase == GamePhase.WaitingForPlayers && room.Players.Count >= room.MinPlayersRequired)
            {
                room.Phase = GamePhase.Betting;
            }
        }
    }

    public void StartGame(string roomId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return;

            if (room.Mode == GameMode.Poker)
            {
                StartPokerGame(room);
            }
            else
            {
                StartBlackjackGame(room);
            }
        }
    }

    private void StartBlackjackGame(GameRoom room)
    {
        room.Phase = GamePhase.Dealing;
        room.Deck = CreateDeck();
        ShuffleDeck(room.Deck);
        room.DealerHand.Clear();

        foreach (var player in room.Players)
        {
            player.Hand.Clear();
            player.Status = PlayerStatus.Playing;
        }

        // Deal 2 cards to each player
        for (int i = 0; i < 2; i++)
        {
            foreach (var player in room.Players)
            {
                player.Hand.Add(DrawCard(room));
            }
            
            var dealerCard = DrawCard(room);
            if (i == 1) dealerCard.IsFaceUp = false; // Second dealer card face down
            room.DealerHand.Add(dealerCard);
        }

        // Check for blackjacks
        foreach (var player in room.Players)
        {
            if (CalculateHandValue(player.Hand) == 21)
            {
                player.Status = PlayerStatus.Blackjack;
            }
        }

        room.CurrentPlayerIndex = 0;
        AdvanceToNextActivePlayer(room);
        
        if (room.Players.All(p => p.Status != PlayerStatus.Playing))
        {
            room.Phase = GamePhase.DealerTurn;
        }
        else
        {
            room.Phase = GamePhase.PlayerTurn;
        }
    }

    public (bool success, CardInfo? card) Hit(string roomId, string connectionId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return (false, null);

            if (room.CurrentPlayer?.ConnectionId != connectionId)
                return (false, null);

            var player = room.CurrentPlayer;
            var card = DrawCard(room);
            player.Hand.Add(card);

            var handValue = CalculateHandValue(player.Hand);
            if (handValue > 21)
            {
                player.Status = PlayerStatus.Busted;
                AdvanceToNextActivePlayer(room);
            }
            else if (handValue == 21)
            {
                player.Status = PlayerStatus.Standing;
                AdvanceToNextActivePlayer(room);
            }

            return (true, card);
        }
    }

    public bool Stand(string roomId, string connectionId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return false;

            if (room.CurrentPlayer?.ConnectionId != connectionId)
                return false;

            room.CurrentPlayer.Status = PlayerStatus.Standing;
            AdvanceToNextActivePlayer(room);

            return true;
        }
    }

    public void PlayDealerTurn(string roomId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return;

            room.Phase = GamePhase.DealerTurn;

            // Reveal hidden card
            foreach (var card in room.DealerHand)
            {
                card.IsFaceUp = true;
            }

            // Dealer draws until 17 or higher
            while (CalculateHandValue(room.DealerHand) < 17)
            {
                room.DealerHand.Add(DrawCard(room));
            }

            // Determine winners
            var dealerValue = CalculateHandValue(room.DealerHand);
            var dealerBusted = dealerValue > 21;

            foreach (var player in room.Players)
            {
                if (player.Status == PlayerStatus.Busted)
                {
                    player.Status = PlayerStatus.Lost;
                    continue;
                }

                var playerValue = CalculateHandValue(player.Hand);

                if (player.Status == PlayerStatus.Blackjack)
                {
                    if (dealerValue == 21 && room.DealerHand.Count == 2)
                    {
                        player.Status = PlayerStatus.Push;
                        player.Balance += player.CurrentBet;
                    }
                    else
                    {
                        player.Status = PlayerStatus.Won;
                        player.Balance += (int)(player.CurrentBet * 2.5); // Blackjack pays 3:2
                    }
                }
                else if (dealerBusted || playerValue > dealerValue)
                {
                    player.Status = PlayerStatus.Won;
                    player.Balance += player.CurrentBet * 2;
                }
                else if (playerValue == dealerValue)
                {
                    player.Status = PlayerStatus.Push;
                    player.Balance += player.CurrentBet;
                }
                else
                {
                    player.Status = PlayerStatus.Lost;
                }
            }

            room.Phase = GamePhase.GameOver;
        }
    }

    public void ResetForNewRound(string roomId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return;

            room.Phase = GamePhase.Betting;
            room.DealerHand.Clear();
            room.CurrentPlayerIndex = 0;

            foreach (var player in room.Players)
            {
                player.Hand.Clear();
                player.CurrentBet = 0;
                player.Status = PlayerStatus.Waiting;
                player.IsReady = false;
            }

            // Remove players with no balance
            room.Players.RemoveAll(p => p.Balance <= 0);
        }
    }

    public GameStateDto GetGameState(string roomId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return new GameStateDto();

            return new GameStateDto
            {
                RoomId = room.RoomId,
                Mode = room.Mode,
                Phase = room.Phase,
                Players = room.Players.Select(p => new PlayerDto
                {
                    ConnectionId = p.ConnectionId,
                    Username = p.Username,
                    Balance = p.Balance,
                    CurrentBet = p.CurrentBet,
                    TotalBetThisRound = p.TotalBetThisRound,
                    Hand = p.Hand,
                    HandValue = room.Mode == GameMode.Poker ? 0 : CalculateHandValue(p.Hand),
                    HandDescription = p.HandDescription,
                    SeatPosition = p.SeatPosition,
                    Status = p.Status,
                    IsFolded = p.IsFolded,
                    IsAllIn = p.IsAllIn
                }).ToList(),
                DealerHand = room.DealerHand,
                CommunityCards = room.CommunityCards,
                DealerHandValue = CalculateHandValue(room.DealerHand.Where(c => c.IsFaceUp).ToList()),
                Pot = room.Pot,
                CurrentBetToMatch = room.CurrentBetToMatch,
                MinRaise = room.BigBlindAmount,
                CurrentPlayerId = room.CurrentPlayer?.ConnectionId,
                Message = GetPhaseMessage(room),
                MinPlayersRequired = room.MinPlayersRequired,
                CurrentPlayerCount = room.Players.Count,
                PokerRound = room.PokerRound,
                DealerButtonIndex = room.DealerButtonIndex
            };
        }
    }

    private void AdvanceToNextActivePlayer(GameRoom room)
    {
        var startIndex = room.CurrentPlayerIndex;
        do
        {
            room.CurrentPlayerIndex++;
            if (room.CurrentPlayerIndex >= room.Players.Count)
            {
                room.Phase = GamePhase.DealerTurn;
                return;
            }
        } while (room.Players[room.CurrentPlayerIndex].Status != PlayerStatus.Playing &&
                 room.CurrentPlayerIndex != startIndex);

        if (room.Players[room.CurrentPlayerIndex].Status != PlayerStatus.Playing)
        {
            room.Phase = GamePhase.DealerTurn;
        }
    }

    private static List<CardInfo> CreateDeck()
    {
        var deck = new List<CardInfo>();
        foreach (var suit in Suits)
        {
            foreach (var value in Values)
            {
                deck.Add(new CardInfo { Suit = suit, Value = value });
            }
        }
        return deck;
    }

    private static void ShuffleDeck(List<CardInfo> deck)
    {
        var rng = Random.Shared;
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    private static CardInfo DrawCard(GameRoom room)
    {
        if (room.Deck.Count == 0)
        {
            room.Deck = CreateDeck();
            ShuffleDeck(room.Deck);
        }

        var card = room.Deck[0];
        room.Deck.RemoveAt(0);
        return card;
    }

    public static int CalculateHandValue(List<CardInfo> hand)
    {
        int total = 0;
        int aces = 0;

        foreach (var card in hand.Where(c => c.IsFaceUp))
        {
            if (card.Value == "Ace")
            {
                aces++;
                total += 11;
            }
            else
            {
                total += card.GetNumericValue();
            }
        }

        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }

        return total;
    }

    private static string GetPhaseMessage(GameRoom room)
    {
        if (room.Phase == GamePhase.WaitingForPlayers || room.Phase == GamePhase.Betting)
        {
            if (room.Players.Count < room.MinPlayersRequired)
            {
                var needed = room.MinPlayersRequired - room.Players.Count;
                var gameType = room.Mode == GameMode.Poker ? "Poker" : "Blackjack";
                return $"Waiting for {needed} more player(s) to start {gameType}...";
            }
        }

        return room.Phase switch
        {
            GamePhase.WaitingForPlayers => "Waiting for players to join...",
            GamePhase.Betting => room.Mode == GameMode.Poker ? GetPokerPhaseMessage(room) : "Place your bets!",
            GamePhase.Dealing => "Dealing cards...",
            GamePhase.PlayerTurn => room.Mode == GameMode.Poker ? GetPokerTurnMessage(room) : $"{room.CurrentPlayer?.Username}'s turn",
            GamePhase.DealerTurn => room.Mode == GameMode.Poker ? "Showdown!" : "Dealer's turn",
            GamePhase.Showdown => "Showdown!",
            GamePhase.GameOver => "Game Over",
            _ => ""
        };
    }

    private static string GetPokerPhaseMessage(GameRoom room)
    {
        return room.PokerRound switch
        {
            PokerRound.PreFlop => "Pre-Flop betting",
            PokerRound.Flop => "Flop betting",
            PokerRound.Turn => "Turn betting",
            PokerRound.River => "River betting",
            PokerRound.Showdown => "Showdown!",
            _ => "Place your bets!"
        };
    }

    private static string GetPokerTurnMessage(GameRoom room)
    {
        var player = room.CurrentPlayer;
        if (player == null) return "";
        
        var toCall = room.CurrentBetToMatch - player.TotalBetThisRound;
        if (toCall > 0)
            return $"{player.Username}'s turn (${toCall} to call)";
        return $"{player.Username}'s turn (check or raise)";
    }

    public string? FindRoomByConnection(string connectionId)
    {
        lock (_lock)
        {
            return _rooms.Values
                .FirstOrDefault(r => r.Players.Any(p => p.ConnectionId == connectionId))
                ?.RoomId;
        }
    }

    #region Poker Logic

    private void StartPokerGame(GameRoom room)
    {
        room.Phase = GamePhase.Dealing;
        room.Deck = CreateDeck();
        ShuffleDeck(room.Deck);
        room.CommunityCards.Clear();
        room.Pot = 0;
        room.CurrentBetToMatch = 0;
        room.PokerRound = PokerRound.PreFlop;

        foreach (var player in room.Players)
        {
            player.Hand.Clear();
            player.Status = PlayerStatus.Playing;
            player.IsFolded = false;
            player.IsAllIn = false;
            player.TotalBetThisRound = 0;
            player.HasActedThisRound = false;
            player.HandDescription = null;
        }

        // Deal 2 hole cards to each player (face down for others)
        for (int i = 0; i < 2; i++)
        {
            foreach (var player in room.Players)
            {
                var card = DrawCard(room);
                card.IsFaceUp = true; // Player can see their own cards
                player.Hand.Add(card);
            }
        }

        // Post blinds
        var smallBlindIndex = (room.DealerButtonIndex + 1) % room.Players.Count;
        var bigBlindIndex = (room.DealerButtonIndex + 2) % room.Players.Count;

        PostBlind(room, room.Players[smallBlindIndex], room.SmallBlindAmount);
        PostBlind(room, room.Players[bigBlindIndex], room.BigBlindAmount);

        room.CurrentBetToMatch = room.BigBlindAmount;

        // Action starts left of big blind
        room.CurrentPlayerIndex = (bigBlindIndex + 1) % room.Players.Count;
        
        room.Phase = GamePhase.PlayerTurn;
    }

    private void PostBlind(GameRoom room, PlayerInfo player, int amount)
    {
        var actualAmount = Math.Min(amount, player.Balance);
        player.Balance -= actualAmount;
        player.TotalBetThisRound = actualAmount;
        player.CurrentBet = actualAmount;
        room.Pot += actualAmount;

        if (player.Balance == 0)
            player.IsAllIn = true;
    }

    public bool PokerFold(string roomId, string connectionId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || room.Mode != GameMode.Poker)
                return false;

            if (room.CurrentPlayer?.ConnectionId != connectionId)
                return false;

            var player = room.CurrentPlayer;
            player.IsFolded = true;
            player.Status = PlayerStatus.Lost;

            // Check if only one player left
            var activePlayers = room.ActivePlayers;
            if (activePlayers.Count == 1)
            {
                // Winner takes all
                var winner = activePlayers[0];
                winner.Balance += room.Pot;
                winner.Status = PlayerStatus.Won;
                room.Pot = 0;
                room.Phase = GamePhase.GameOver;
                return true;
            }

            AdvancePokerTurn(room);
            return true;
        }
    }

    public bool PokerCheck(string roomId, string connectionId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || room.Mode != GameMode.Poker)
                return false;

            if (room.CurrentPlayer?.ConnectionId != connectionId)
                return false;

            var player = room.CurrentPlayer;
            
            // Can only check if no bet to match
            if (room.CurrentBetToMatch > player.TotalBetThisRound)
                return false;

            player.HasActedThisRound = true;
            AdvancePokerTurn(room);
            return true;
        }
    }

    public bool PokerCall(string roomId, string connectionId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || room.Mode != GameMode.Poker)
                return false;

            if (room.CurrentPlayer?.ConnectionId != connectionId)
                return false;

            var player = room.CurrentPlayer;
            var toCall = room.CurrentBetToMatch - player.TotalBetThisRound;
            
            if (toCall <= 0)
                return PokerCheck(roomId, connectionId);

            var actualCall = Math.Min(toCall, player.Balance);
            player.Balance -= actualCall;
            player.TotalBetThisRound += actualCall;
            player.CurrentBet = actualCall;
            room.Pot += actualCall;

            if (player.Balance == 0)
                player.IsAllIn = true;

            player.HasActedThisRound = true;
            AdvancePokerTurn(room);
            return true;
        }
    }

    public bool PokerRaise(string roomId, string connectionId, int raiseAmount)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || room.Mode != GameMode.Poker)
                return false;

            if (room.CurrentPlayer?.ConnectionId != connectionId)
                return false;

            var player = room.CurrentPlayer;
            var toCall = room.CurrentBetToMatch - player.TotalBetThisRound;
            var totalNeeded = toCall + raiseAmount;

            if (totalNeeded > player.Balance)
                return false;

            if (raiseAmount < room.BigBlindAmount && raiseAmount < player.Balance)
                return false; // Minimum raise is big blind

            player.Balance -= totalNeeded;
            player.TotalBetThisRound += totalNeeded;
            player.CurrentBet = totalNeeded;
            room.Pot += totalNeeded;
            room.CurrentBetToMatch = player.TotalBetThisRound;

            if (player.Balance == 0)
                player.IsAllIn = true;

            // Reset has acted for other players since there's a raise
            foreach (var p in room.ActivePlayers.Where(p => p != player))
            {
                p.HasActedThisRound = false;
            }

            player.HasActedThisRound = true;
            AdvancePokerTurn(room);
            return true;
        }
    }

    public bool PokerAllIn(string roomId, string connectionId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || room.Mode != GameMode.Poker)
                return false;

            if (room.CurrentPlayer?.ConnectionId != connectionId)
                return false;

            var player = room.CurrentPlayer;
            var allInAmount = player.Balance;

            player.TotalBetThisRound += allInAmount;
            player.CurrentBet = allInAmount;
            room.Pot += allInAmount;
            player.Balance = 0;
            player.IsAllIn = true;

            if (player.TotalBetThisRound > room.CurrentBetToMatch)
            {
                room.CurrentBetToMatch = player.TotalBetThisRound;
                // Reset has acted for other players
                foreach (var p in room.ActivePlayers.Where(p => p != player))
                {
                    p.HasActedThisRound = false;
                }
            }

            player.HasActedThisRound = true;
            AdvancePokerTurn(room);
            return true;
        }
    }

    private void AdvancePokerTurn(GameRoom room)
    {
        // Get players who can still act (not folded, not all-in)
        var playersWhoCanAct = room.Players.Where(p => !p.IsFolded && !p.IsAllIn).ToList();
        
        // If only one player can act and they've matched the bet, or no one can act, move to next round
        if (playersWhoCanAct.Count == 0)
        {
            // Everyone is all-in or folded, deal remaining cards
            AdvancePokerRound(room);
            return;
        }

        // Check if betting round is complete (all active players have acted and matched the bet)
        var allPlayersActed = playersWhoCanAct.All(p => p.HasActedThisRound);
        var allBetsMatched = playersWhoCanAct.All(p => p.TotalBetThisRound == room.CurrentBetToMatch);
        
        if (allPlayersActed && allBetsMatched)
        {
            // Betting round complete, advance to next round
            AdvancePokerRound(room);
            return;
        }

        // Find next player who can act
        var startIndex = room.CurrentPlayerIndex;
        var iterations = 0;
        do
        {
            room.CurrentPlayerIndex = (room.CurrentPlayerIndex + 1) % room.Players.Count;
            var nextPlayer = room.Players[room.CurrentPlayerIndex];
            
            if (!nextPlayer.IsFolded && !nextPlayer.IsAllIn)
            {
                // Check if this player still needs to act
                if (!nextPlayer.HasActedThisRound || nextPlayer.TotalBetThisRound < room.CurrentBetToMatch)
                {
                    return; // Found next player who needs to act
                }
            }
            iterations++;
        } while (iterations < room.Players.Count);

        // No more players need to act, advance round
        AdvancePokerRound(room);
    }

    private void AdvancePokerRound(GameRoom room)
    {
        // Reset for new betting round
        foreach (var player in room.Players)
        {
            player.HasActedThisRound = false;
            player.TotalBetThisRound = 0;
            player.CurrentBet = 0;
        }
        room.CurrentBetToMatch = 0;

        // Set action to first active player after dealer
        room.CurrentPlayerIndex = (room.DealerButtonIndex + 1) % room.Players.Count;
        while (room.Players[room.CurrentPlayerIndex].IsFolded || room.Players[room.CurrentPlayerIndex].IsAllIn)
        {
            room.CurrentPlayerIndex = (room.CurrentPlayerIndex + 1) % room.Players.Count;
        }

        switch (room.PokerRound)
        {
            case PokerRound.PreFlop:
                room.PokerRound = PokerRound.Flop;
                DealCommunityCards(room, 3); // Flop: 3 cards
                break;

            case PokerRound.Flop:
                room.PokerRound = PokerRound.Turn;
                DealCommunityCards(room, 1); // Turn: 1 card
                break;

            case PokerRound.Turn:
                room.PokerRound = PokerRound.River;
                DealCommunityCards(room, 1); // River: 1 card
                break;

            case PokerRound.River:
                room.PokerRound = PokerRound.Showdown;
                room.Phase = GamePhase.Showdown;
                DeterminePokerWinner(room);
                break;
        }

        // Check if only one player remains or all others all-in
        if (room.ActivePlayers.Count(p => !p.IsAllIn) <= 1 && room.PokerRound != PokerRound.Showdown)
        {
            // Deal remaining community cards and go to showdown
            while (room.CommunityCards.Count < 5)
            {
                DealCommunityCards(room, 1);
            }
            room.PokerRound = PokerRound.Showdown;
            room.Phase = GamePhase.Showdown;
            DeterminePokerWinner(room);
        }
    }

    private void DealCommunityCards(GameRoom room, int count)
    {
        // Burn a card
        DrawCard(room);

        for (int i = 0; i < count; i++)
        {
            var card = DrawCard(room);
            card.IsFaceUp = true;
            room.CommunityCards.Add(card);
        }
    }

    private void DeterminePokerWinner(GameRoom room)
    {
        var activePlayers = room.Players.Where(p => !p.IsFolded).ToList();

        if (activePlayers.Count == 1)
        {
            var winner = activePlayers[0];
            winner.Balance += room.Pot;
            winner.Status = PlayerStatus.Won;
            room.Phase = GamePhase.GameOver;
            return;
        }

        // Evaluate hands
        var playerHands = new List<(PlayerInfo Player, PokerHandEvaluator.HandResult Hand)>();
        
        foreach (var player in activePlayers)
        {
            var allCards = player.Hand
                .Concat(room.CommunityCards)
                .Select(c => (c.Suit, c.Value))
                .ToList();
            
            var handResult = PokerHandEvaluator.EvaluateHand(allCards);
            player.HandDescription = handResult.Description;
            playerHands.Add((player, handResult));
        }

        // Sort by hand strength
        playerHands = playerHands.OrderByDescending(ph => ph.Hand, 
            Comparer<PokerHandEvaluator.HandResult>.Create((a, b) => a.CompareTo(b))).ToList();

        // Find winners (could be multiple if tie)
        var bestHand = playerHands[0].Hand;
        var winners = playerHands.Where(ph => ph.Hand.CompareTo(bestHand) == 0).Select(ph => ph.Player).ToList();

        // Split pot among winners
        var winAmount = room.Pot / winners.Count;
        foreach (var winner in winners)
        {
            winner.Balance += winAmount;
            winner.Status = PlayerStatus.Won;
        }

        // Mark others as lost
        foreach (var player in activePlayers.Where(p => !winners.Contains(p)))
        {
            player.Status = PlayerStatus.Lost;
        }

        room.Pot = 0;
        room.Phase = GamePhase.GameOver;
    }

    public void ResetPokerForNewRound(string roomId)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return;

            // Move dealer button
            room.DealerButtonIndex = (room.DealerButtonIndex + 1) % room.Players.Count;

            room.Phase = GamePhase.Betting;
            room.CommunityCards.Clear();
            room.DealerHand.Clear();
            room.Pot = 0;
            room.CurrentBetToMatch = 0;
            room.PokerRound = PokerRound.PreFlop;

            foreach (var player in room.Players)
            {
                player.Hand.Clear();
                player.CurrentBet = 0;
                player.TotalBetThisRound = 0;
                player.Status = PlayerStatus.Waiting;
                player.IsReady = false;
                player.IsFolded = false;
                player.IsAllIn = false;
                player.HasActedThisRound = false;
                player.HandDescription = null;
            }

            // Remove players with no balance
            room.Players.RemoveAll(p => p.Balance <= 0);
        }
    }

    #endregion
}
