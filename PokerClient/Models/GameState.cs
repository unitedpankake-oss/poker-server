using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlackjackGame.Models
{
    public class GameState : INotifyPropertyChanged
    {
        private GameMode _currentMode = GameMode.Blackjack;
        private GamePhase _phase = GamePhase.Betting;
        private Player? _currentPlayer;
        private Player _dealer = new() { Username = "Dealer", IsDealer = true };
        private List<Player> _players = [];
        private List<Card> _communityCards = [];
        private int _pot;
        private string _message = string.Empty;

        public GameMode CurrentMode
        {
            get => _currentMode;
            set { _currentMode = value; OnPropertyChanged(); }
        }

        public GamePhase Phase
        {
            get => _phase;
            set { _phase = value; OnPropertyChanged(); OnPropertyChanged(nameof(PhaseText)); }
        }

        public string PhaseText => Phase switch
        {
            GamePhase.WaitingForPlayers => "Waiting for Players...",
            GamePhase.Betting => "Place Your Bets",
            GamePhase.Dealing => "Dealing Cards...",
            GamePhase.PlayerTurn => $"{CurrentPlayer?.Username}'s Turn",
            GamePhase.DealerTurn => "Dealer's Turn",
            GamePhase.Showdown => "Showdown",
            GamePhase.GameOver => "Game Over",
            _ => ""
        };

        public Player? CurrentPlayer
        {
            get => _currentPlayer;
            set { _currentPlayer = value; OnPropertyChanged(); OnPropertyChanged(nameof(PhaseText)); }
        }

        public Player Dealer
        {
            get => _dealer;
            set { _dealer = value; OnPropertyChanged(); }
        }

        public List<Player> Players
        {
            get => _players;
            set { _players = value; OnPropertyChanged(); }
        }

        public List<Card> CommunityCards
        {
            get => _communityCards;
            set { _communityCards = value; OnPropertyChanged(); }
        }

        public int Pot
        {
            get => _pot;
            set { _pot = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedPot)); }
        }

        public string FormattedPot => $"${Pot:N0}";

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
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
}
