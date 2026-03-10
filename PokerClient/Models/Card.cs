namespace BlackjackGame.Models
{
    public class Card
    {
        public string Value { get; set; } = string.Empty;
        public string Suit { get; set; } = string.Empty;
        public bool IsFaceUp { get; set; } = true;

        public string DisplayValue => Value switch
        {
            "Jack" => "J",
            "Queen" => "Q",
            "King" => "K",
            "Ace" => "A",
            _ => Value
        };

        public string SuitSymbol => Suit switch
        {
            "Hearts" => "♥",
            "Diamonds" => "♦",
            "Clubs" => "♣",
            "Spades" => "♠",
            _ => ""
        };

        public bool IsRed => Suit == "Hearts" || Suit == "Diamonds";

        public int GetValue(int currentHandValue = 0)
        {
            return Value switch
            {
                "Jack" or "Queen" or "King" => 10,
                "Ace" => currentHandValue + 11 > 21 ? 1 : 11,
                _ => int.Parse(Value)
            };
        }
    }

    public class Deck
    {
        private readonly List<Card> _cards = [];
        private int _currentIndex;
        private readonly Random _rng = new();

        public Deck()
        {
            Initialize();
            Shuffle();
        }

        public void Initialize()
        {
            _cards.Clear();
            string[] suits = ["Hearts", "Diamonds", "Clubs", "Spades"];
            string[] values = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace"];

            foreach (var suit in suits)
            {
                foreach (var value in values)
                {
                    _cards.Add(new Card { Suit = suit, Value = value });
                }
            }
        }

        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
            _currentIndex = 0;
        }

        public Card DrawCard()
        {
            if (_currentIndex >= _cards.Count)
            {
                Initialize();
                Shuffle();
            }
            return _cards[_currentIndex++];
        }

        public int RemainingCards => _cards.Count - _currentIndex;
    }
}
