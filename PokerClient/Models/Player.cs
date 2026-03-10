using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlackjackGame.Models
{
    public class Player : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _username = string.Empty;
        private string _avatarUrl = string.Empty;
        private int _balance;
        private int _currentBet;
        private List<Card> _hand = [];
        private bool _isActive;
        private bool _isDealer;
        private int _seatPosition;
        private PlayerStatus _status = PlayerStatus.Waiting;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string AvatarUrl
        {
            get => _avatarUrl;
            set { _avatarUrl = value; OnPropertyChanged(); }
        }

        public int Balance
        {
            get => _balance;
            set { _balance = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedBalance)); }
        }

        public string FormattedBalance => $"${Balance:N0}";

        public int CurrentBet
        {
            get => _currentBet;
            set { _currentBet = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedBet)); }
        }

        public string FormattedBet => $"${CurrentBet:N0}";

        public List<Card> Hand
        {
            get => _hand;
            set { _hand = value; OnPropertyChanged(); OnPropertyChanged(nameof(HandValue)); }
        }

        public int HandValue
        {
            get
            {
                int total = 0;
                int aces = 0;
                foreach (var card in Hand.Where(c => c.IsFaceUp))
                {
                    if (card.Value == "Ace") aces++;
                    total += card.GetValue(total);
                }
                while (total > 21 && aces > 0)
                {
                    total -= 10;
                    aces--;
                }
                return total;
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public bool IsDealer
        {
            get => _isDealer;
            set { _isDealer = value; OnPropertyChanged(); }
        }

        public int SeatPosition
        {
            get => _seatPosition;
            set { _seatPosition = value; OnPropertyChanged(); }
        }

        public PlayerStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => Status switch
        {
            PlayerStatus.Playing => "Playing",
            PlayerStatus.Stand => "Stand",
            PlayerStatus.Bust => "Bust",
            PlayerStatus.Blackjack => "Blackjack!",
            PlayerStatus.Winner => "Winner!",
            PlayerStatus.Loser => "Lost",
            PlayerStatus.Push => "Push",
            _ => ""
        };

        public void ClearHand()
        {
            Hand.Clear();
            CurrentBet = 0;
            Status = PlayerStatus.Waiting;
            OnPropertyChanged(nameof(Hand));
            OnPropertyChanged(nameof(HandValue));
        }

        public void AddCard(Card card)
        {
            Hand.Add(card);
            OnPropertyChanged(nameof(Hand));
            OnPropertyChanged(nameof(HandValue));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum PlayerStatus
    {
        Waiting,
        Betting,
        Playing,
        Stand,
        Bust,
        Blackjack,
        Winner,
        Loser,
        Push
    }
}
