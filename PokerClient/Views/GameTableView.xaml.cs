using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BlackjackGame.Models;

namespace BlackjackGame.Views
{
    public partial class GameTableView : UserControl
    {
        private UserAccount _currentUser = null!;
        private readonly Deck _deck = new();
        private readonly Player _player = new();
        private readonly Player _dealer = new() { IsDealer = true, Username = "Dealer" };
        private readonly List<GameHistoryEntry> _gameHistory = [];
        private GameMode _currentMode = GameMode.Blackjack;
        private int _currentBet;
        private bool _isGameActive;

        public event EventHandler? LogoutRequested;
        public event EventHandler<UserAccount>? UserUpdated;


        public GameTableView()
        {
            InitializeComponent();
            Loaded += GameTableView_Loaded;
        }

        private void GameTableView_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize UI after visual tree is loaded
            if (_currentUser != null)
            {
                ResetForNewGame();
            }
        }

        public void Initialize(UserAccount user)
        {
            _currentUser = user;
            _player.Username = user.Username;
            _player.Balance = user.Balance;
            
            UpdateUI();
            
            // Only reset if already loaded, otherwise Loaded event will handle it
            if (IsLoaded)
            {
                ResetForNewGame();
            }
        }

        private void UpdateUI()
        {
            if (UsernameDisplay == null) return;
            UsernameDisplay.Text = _currentUser.Username;
            BalanceDisplay.Text = _currentUser.FormattedBalance;
        }


        private void ResetForNewGame()
        {
            _currentBet = 0;
            _isGameActive = false;
            
            _player.ClearHand();
            _dealer.ClearHand();
            _deck.Shuffle();

            // Check if UI elements are loaded before accessing them
            if (PlayerCardsPanel == null) return;

            PlayerCardsPanel.Children.Clear();
            DealerCardsPanel.Children.Clear();
            
            CurrentBetDisplay.Text = "$0";
            GameMessage.Text = "Place Your Bet";
            
            PlayerValueBadge.Visibility = Visibility.Collapsed;
            DealerValueBadge.Visibility = Visibility.Collapsed;
            
            BettingPanel.IsEnabled = true;
            DealButton.IsEnabled = false;
            DealButton.Visibility = Visibility.Visible;
            ActionButtonsPanel.Visibility = Visibility.Collapsed;
            NewGameButton.Visibility = Visibility.Collapsed;
            ResultOverlay.Visibility = Visibility.Collapsed;

            UpdateChipButtons();
        }

        private void UpdateChipButtons()
        {
            Chip10.IsEnabled = _currentUser.Balance >= 10;
            Chip25.IsEnabled = _currentUser.Balance >= 25;
            Chip50.IsEnabled = _currentUser.Balance >= 50;
            Chip100.IsEnabled = _currentUser.Balance >= 100;
            Chip500.IsEnabled = _currentUser.Balance >= 500;
        }

        private void ChipButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagValue && int.TryParse(tagValue, out int chipValue))
            {
                if (_currentBet + chipValue <= _currentUser.Balance)
                {
                    _currentBet += chipValue;
                    CurrentBetDisplay.Text = $"${_currentBet:N0}";
                    DealButton.IsEnabled = _currentBet > 0;
                    
                    AnimateChipAdd();
                }
            }
        }

        private void AnimateChipAdd()
        {
            var animation = new DoubleAnimation(1.1, 1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            CurrentBetDisplay.RenderTransform = new ScaleTransform(1, 1);
            CurrentBetDisplay.RenderTransformOrigin = new Point(0.5, 0.5);
            ((ScaleTransform)CurrentBetDisplay.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            ((ScaleTransform)CurrentBetDisplay.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void ClearBetButton_Click(object sender, RoutedEventArgs e)
        {
            _currentBet = 0;
            CurrentBetDisplay.Text = "$0";
            DealButton.IsEnabled = false;
        }

        private async void DealButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBet <= 0 || _currentBet > _currentUser.Balance) return;

            _currentUser.Balance -= _currentBet;
            UpdateUI();
            
            _isGameActive = true;
            BettingPanel.IsEnabled = false;
            DealButton.Visibility = Visibility.Collapsed;
            
            GameMessage.Text = "Dealing...";

            // Deal initial cards with animation
            await DealInitialCards();

            // Check for blackjack
            if (_player.HandValue == 21)
            {
                await Task.Delay(500);
                EndGame(GameResult.Blackjack);
                return;
            }

            GameMessage.Text = "Your Turn";
            ActionButtonsPanel.Visibility = Visibility.Visible;
            DoubleButton.IsEnabled = _currentUser.Balance >= _currentBet;
            
            UpdateHandValues();
        }

        private async Task DealInitialCards()
        {
            // Player card 1
            var playerCard1 = _deck.DrawCard();
            _player.AddCard(playerCard1);
            await AddCardToPanel(PlayerCardsPanel, playerCard1, 0);

            // Dealer card 1 (face up)
            var dealerCard1 = _deck.DrawCard();
            _dealer.AddCard(dealerCard1);
            await AddCardToPanel(DealerCardsPanel, dealerCard1, 150);

            // Player card 2
            var playerCard2 = _deck.DrawCard();
            _player.AddCard(playerCard2);
            await AddCardToPanel(PlayerCardsPanel, playerCard2, 300);

            // Dealer card 2 (face down)
            var dealerCard2 = _deck.DrawCard();
            dealerCard2.IsFaceUp = false;
            _dealer.AddCard(dealerCard2);
            await AddCardToPanel(DealerCardsPanel, dealerCard2, 450);

            await Task.Delay(200);
        }

        private async Task AddCardToPanel(StackPanel panel, Card card, int delay)
        {
            await Task.Delay(delay);
            
            var cardControl = new CardControl { Card = card, Margin = new Thickness(-20, 0, 0, 0) };
            if (panel.Children.Count == 0)
                cardControl.Margin = new Thickness(0);
            
            panel.Children.Add(cardControl);
            cardControl.AnimateDeal();
        }

        private void UpdateHandValues()
        {
            PlayerHandValue.Text = _player.HandValue.ToString();
            PlayerValueBadge.Visibility = Visibility.Visible;
            
            // Only show visible dealer cards value
            int dealerVisibleValue = _dealer.Hand.Where(c => c.IsFaceUp).Sum(c => c.GetValue());
            DealerHandValue.Text = dealerVisibleValue.ToString();
            DealerValueBadge.Visibility = Visibility.Visible;
        }

        private async void HitButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isGameActive) return;

            var card = _deck.DrawCard();
            _player.AddCard(card);
            await AddCardToPanel(PlayerCardsPanel, card, 0);
            
            UpdateHandValues();

            if (_player.HandValue > 21)
            {
                await Task.Delay(300);
                EndGame(GameResult.Bust);
            }
            else if (_player.HandValue == 21)
            {
                await Task.Delay(300);
                await StandButton_ClickAsync();
            }

            // Disable double after hit
            DoubleButton.IsEnabled = false;
        }

        private async void StandButton_Click(object sender, RoutedEventArgs e)
        {
            await StandButton_ClickAsync();
        }

        private async Task StandButton_ClickAsync()
        {
            if (!_isGameActive) return;

            ActionButtonsPanel.Visibility = Visibility.Collapsed;
            GameMessage.Text = "Dealer's Turn";

            // Flip dealer's hidden card
            if (_dealer.Hand.Count > 1 && !_dealer.Hand[1].IsFaceUp)
            {
                _dealer.Hand[1].IsFaceUp = true;
                if (DealerCardsPanel.Children.Count > 1 && DealerCardsPanel.Children[1] is CardControl cardControl)
                {
                    cardControl.AnimateFlip();
                    await Task.Delay(400);
                }
            }

            // Dealer draws
            while (_dealer.HandValue < 17)
            {
                await Task.Delay(600);
                var card = _deck.DrawCard();
                _dealer.AddCard(card);
                await AddCardToPanel(DealerCardsPanel, card, 0);
            }

            UpdateDealerHandValue();
            await Task.Delay(500);
            
            DetermineWinner();
        }

        private void UpdateDealerHandValue()
        {
            DealerHandValue.Text = _dealer.HandValue.ToString();
        }

        private async void DoubleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isGameActive || _currentUser.Balance < _currentBet) return;

            _currentUser.Balance -= _currentBet;
            _currentBet *= 2;
            CurrentBetDisplay.Text = $"${_currentBet:N0}";
            UpdateUI();

            // Draw one card and stand
            var card = _deck.DrawCard();
            _player.AddCard(card);
            await AddCardToPanel(PlayerCardsPanel, card, 0);
            
            UpdateHandValues();

            if (_player.HandValue > 21)
            {
                await Task.Delay(300);
                EndGame(GameResult.Bust);
            }
            else
            {
                await Task.Delay(300);
                await StandButton_ClickAsync();
            }
        }

        private void DetermineWinner()
        {
            if (_dealer.HandValue > 21)
            {
                EndGame(GameResult.DealerBust);
            }
            else if (_player.HandValue > _dealer.HandValue)
            {
                EndGame(GameResult.Win);
            }
            else if (_dealer.HandValue > _player.HandValue)
            {
                EndGame(GameResult.Lose);
            }
            else
            {
                EndGame(GameResult.Push);
            }
        }

        private void EndGame(GameResult result)
        {
            _isGameActive = false;
            ActionButtonsPanel.Visibility = Visibility.Collapsed;
            
            int winAmount = 0;
            string emoji;
            string title;
            Brush amountColor;

            switch (result)
            {
                case GameResult.Blackjack:
                    winAmount = (int)(_currentBet * 2.5);
                    emoji = "🎰";
                    title = "BLACKJACK!";
                    amountColor = new SolidColorBrush(Color.FromRgb(80, 200, 120));
                    break;
                case GameResult.Win:
                case GameResult.DealerBust:
                    winAmount = _currentBet * 2;
                    emoji = "🎉";
                    title = result == GameResult.DealerBust ? "DEALER BUSTS!" : "YOU WIN!";
                    amountColor = new SolidColorBrush(Color.FromRgb(80, 200, 120));
                    break;
                case GameResult.Push:
                    winAmount = _currentBet;
                    emoji = "🤝";
                    title = "PUSH";
                    amountColor = new SolidColorBrush(Color.FromRgb(212, 175, 55));
                    break;
                case GameResult.Lose:
                    emoji = "😔";
                    title = "DEALER WINS";
                    amountColor = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    break;
                case GameResult.Bust:
                default:
                    emoji = "💥";
                    title = "BUST!";
                    amountColor = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    break;
            }

            _currentUser.Balance += winAmount;
            _currentUser.GamesPlayed++;
            if (result == GameResult.Win || result == GameResult.Blackjack || result == GameResult.DealerBust)
            {
                _currentUser.GamesWon++;
                _currentUser.TotalWinnings += winAmount - _currentBet;
            }
            
            UpdateUI();
            UserUpdated?.Invoke(this, _currentUser);

            // Add to history
            _gameHistory.Insert(0, new GameHistoryEntry
            {
                Mode = _currentMode,
                Result = title,
                BetAmount = _currentBet,
                WinAmount = winAmount - _currentBet,
                PlayerCards = _player.Hand.Select(c => $"{c.DisplayValue}{c.SuitSymbol}").ToList(),
                DealerCards = _dealer.Hand.Select(c => $"{c.DisplayValue}{c.SuitSymbol}").ToList(),
                PlayerHandValue = _player.HandValue,
                DealerHandValue = _dealer.HandValue
            });

            // Show result overlay
            ResultEmoji.Text = emoji;
            ResultTitle.Text = title;
            ResultAmount.Text = winAmount > _currentBet ? $"+${winAmount - _currentBet:N0}" 
                              : winAmount == _currentBet ? "$0" 
                              : $"-${_currentBet:N0}";
            ResultAmount.Foreground = amountColor;
            
            ShowResultOverlay();
        }

        private void ShowResultOverlay()
        {
            ResultOverlay.Visibility = Visibility.Visible;
            ResultOverlay.Opacity = 0;
            
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            ResultOverlay.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, args) =>
            {
                ResultOverlay.Visibility = Visibility.Collapsed;
                ResetForNewGame();
            };
            ResultOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            ResetForNewGame();
        }

        private void BlackjackMode_Checked(object sender, RoutedEventArgs e)
        {
            _currentMode = GameMode.Blackjack;
            if (MultiplayerSeats != null)
                MultiplayerSeats.Visibility = Visibility.Collapsed;
            if (CommunityCardsPanel != null)
                CommunityCardsPanel.Visibility = Visibility.Collapsed;
            ResetForNewGame();
        }

        private void PokerMode_Checked(object sender, RoutedEventArgs e)
        {
            _currentMode = GameMode.Poker;
            if (MultiplayerSeats != null)
                MultiplayerSeats.Visibility = Visibility.Visible;
            GameMessage.Text = "Poker Mode - Coming Soon!";
            
            // Poker mode would require multiplayer implementation
            BettingPanel.IsEnabled = false;
        }

        private void BonusButton_Click(object sender, RoutedEventArgs e)
        {
            // Daily bonus - simple implementation
            var random = new Random();
            int bonus = random.Next(50, 200);
            _currentUser.Balance += bonus;
            UpdateUI();
            UserUpdated?.Invoke(this, _currentUser);
            
            MessageBox.Show($"🎁 You received a bonus of ${bonus}!", "Daily Bonus", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateHistoryPanel();
            HistoryPanel.Visibility = Visibility.Visible;
        }

        private void CloseHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            HistoryPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateHistoryPanel()
        {
            HistoryList.Children.Clear();
            
            if (_gameHistory.Count == 0)
            {
                HistoryList.Children.Add(new TextBlock
                {
                    Text = "No games played yet",
                    Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176)),
                    FontSize = 14,
                    Margin = new Thickness(0, 20, 0, 0)
                });
                return;
            }

            foreach (var entry in _gameHistory.Take(20))
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftStack = new StackPanel();
                leftStack.Children.Add(new TextBlock
                {
                    Text = entry.Result,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14
                });
                leftStack.Children.Add(new TextBlock
                {
                    Text = $"Bet: ${entry.BetAmount:N0} • {entry.TimeString}",
                    Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176)),
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                var resultText = new TextBlock
                {
                    Text = entry.ResultSummary,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = entry.WinAmount > 0 
                        ? new SolidColorBrush(Color.FromRgb(80, 200, 120))
                        : entry.WinAmount < 0 
                            ? new SolidColorBrush(Color.FromRgb(231, 76, 60))
                            : new SolidColorBrush(Color.FromRgb(212, 175, 55))
                };
                Grid.SetColumn(resultText, 1);

                grid.Children.Add(leftStack);
                grid.Children.Add(resultText);
                border.Child = grid;
                HistoryList.Children.Add(border);
            }
        }
    }

    public enum GameResult
    {
        Win,
        Lose,
        Push,
        Blackjack,
        Bust,
        DealerBust
    }
}
