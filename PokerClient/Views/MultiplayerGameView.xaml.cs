using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BlackjackGame.Models;
using BlackjackGame.Services;

using GamePhase = BlackjackGame.Services.GamePhase;
using PlayerStatus = BlackjackGame.Services.PlayerStatus;

namespace BlackjackGame.Views;

public partial class MultiplayerGameView : UserControl
{
    private GameHubService _hubService = null!;
    private UserAccount _currentUser = null!;
    private string _roomId = string.Empty;
    private int _currentBet;
    private GameStateDto? _currentState;
    private DispatcherTimer? _turnTimer;
    private int _remainingSeconds;
    private bool _isSpectator;

    public event EventHandler? LeaveRequested;
    public event EventHandler<UserAccount>? UserUpdated;

    public MultiplayerGameView()
    {
        InitializeComponent();
        InitializeTimer();
    }

    private void InitializeTimer()
    {
        _turnTimer = new DispatcherTimer();
        _turnTimer.Interval = TimeSpan.FromSeconds(1);
        _turnTimer.Tick += TurnTimer_Tick;
    }

    private void TurnTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        TimerText.Text = _remainingSeconds.ToString();
        
        // Change color when low on time
        if (_remainingSeconds <= 10)
        {
            TimerBorder.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Red
            if (_remainingSeconds <= 5)
            {
                // Pulse animation for urgency
                var animation = new DoubleAnimation(0.5, 1.0, TimeSpan.FromMilliseconds(200));
                animation.AutoReverse = true;
                TimerBorder.BeginAnimation(OpacityProperty, animation);
            }
        }
        else
        {
            TimerBorder.Background = new SolidColorBrush(Color.FromArgb(128, 231, 76, 60));
        }

        if (_remainingSeconds <= 0)
        {
            _turnTimer?.Stop();
            TimerBorder.Visibility = Visibility.Collapsed;
        }
    }

    public void Initialize(UserAccount user, string roomId, GameHubService hubService, bool asSpectator = false)
    {
        _currentUser = user;
        _roomId = roomId;
        _hubService = hubService;
        _isSpectator = asSpectator;

        SetupEventHandlers();
        UpdateUI();
    }

    private void SetupEventHandlers()
    {
        _hubService.OnGameStateUpdated += OnGameStateUpdated;
        _hubService.OnGameStarted += OnGameStarted;
        _hubService.OnDealerTurnComplete += OnDealerTurnComplete;
        _hubService.OnNewRoundStarted += OnNewRoundStarted;
        _hubService.OnPlayerLeft += OnPlayerLeft;
        _hubService.OnTurnTimeout += OnTurnTimeout;
        _hubService.OnTurnTimerStarted += OnTurnTimerStarted;
    }

    private void OnTurnTimeout(string connectionId)
    {
        Dispatcher.Invoke(() =>
        {
            _turnTimer?.Stop();
            TimerBorder.Visibility = Visibility.Collapsed;
            
            if (connectionId == _hubService.ConnectionId)
            {
                GameMessage.Text = "⏱ Time's up! Auto-folded.";
            }
        });
    }

    private void OnTurnTimerStarted(string playerId, int seconds)
    {
        Dispatcher.Invoke(() =>
        {
            _remainingSeconds = seconds;
            TimerText.Text = seconds.ToString();
            TimerBorder.Background = new SolidColorBrush(Color.FromArgb(128, 231, 76, 60));
            TimerBorder.Visibility = Visibility.Visible;
            _turnTimer?.Start();
        });
    }

    private void OnGameStateUpdated(GameStateDto state)
    {
        Dispatcher.Invoke(() => UpdateGameState(state));
    }

    private void OnGameStarted()
    {
        Dispatcher.Invoke(() =>
        {
            GameMessage.Text = "Game Started!";
            AnimateMessage();
        });
    }

    private void OnDealerTurnComplete()
    {
        Dispatcher.Invoke(() =>
        {
            GameMessage.Text = "Dealer's turn complete";
        });
    }

    private void OnNewRoundStarted()
    {
        Dispatcher.Invoke(() =>
        {
            _currentBet = 0;
            ResultOverlay.Visibility = Visibility.Collapsed;
            MuckShowPanel.Visibility = Visibility.Collapsed;
            PlayerCardsPanel.Children.Clear();
            DealerCardsPanel.Children.Clear();
            _turnTimer?.Stop();
            TimerBorder.Visibility = Visibility.Collapsed;
            UpdateUI();
        });
    }

    private void OnPlayerLeft(string connectionId)
    {
        Dispatcher.Invoke(() =>
        {
            // Player left notification could be added here
        });
    }


    private void UpdateGameState(GameStateDto state)
    {
        _currentState = state;

        // Find current player (could be in players or spectators)
        var myPlayer = state.Players.FirstOrDefault(p => p.ConnectionId == _hubService.ConnectionId);
        var mySpectator = state.Spectators.FirstOrDefault(p => p.ConnectionId == _hubService.ConnectionId);
        _isSpectator = myPlayer == null && mySpectator != null;

        if (myPlayer != null)
        {
            _currentUser.Balance = myPlayer.Balance;
            BalanceDisplay.Text = $"${myPlayer.Balance:N0}";
        }
        else if (mySpectator != null)
        {
            BalanceDisplay.Text = $"${mySpectator.Balance:N0} (Watching)";
        }

        // Update game message with player count info
        var modeName = state.Mode == 0 ? "Blackjack" : "Poker";
        var roundInfo = state.IsPoker && state.Phase == (int)GamePhase.PlayerTurn ? $" - {state.PokerRoundName}" : "";
        var spectatorInfo = state.Spectators.Count > 0 ? $" | 👁 {state.Spectators.Count}" : "";
        RoomNameDisplay.Text = $"{modeName} ({state.CurrentPlayerCount}/{state.MinPlayersRequired}+ players){spectatorInfo}{roundInfo}";
        GameMessage.Text = state.Message;

        // Update timer visibility
        if (state.Phase == (int)GamePhase.PlayerTurn && state.TurnStartTime != null)
        {
            var elapsed = (DateTime.UtcNow - state.TurnStartTime.Value).TotalSeconds;
            _remainingSeconds = Math.Max(0, state.TurnTimeoutSeconds - (int)elapsed);
            TimerText.Text = _remainingSeconds.ToString();
            TimerBorder.Visibility = Visibility.Visible;
        }
        else
        {
            TimerBorder.Visibility = Visibility.Collapsed;
            _turnTimer?.Stop();
        }

        // Update display based on game mode
        if (state.IsPoker)
        {
            UpdatePokerDisplay(state, myPlayer);
        }
        else
        {
            UpdateBlackjackDisplay(state, myPlayer);
        }

        // Update controls based on phase
        UpdateControlsForPhase((GamePhase)state.Phase, state.CurrentPlayerId == _hubService.ConnectionId, myPlayer, state);
    }

    private void UpdatePokerDisplay(GameStateDto state, PlayerInfo? myPlayer)
    {
        // Show community cards instead of dealer cards
        DealerLabel.Text = "COMMUNITY CARDS";
        DealerCardsPanel.Children.Clear();

        foreach (var card in state.CommunityCards)
        {
            var cardControl = CreateCardVisual(card, true);
            DealerCardsPanel.Children.Add(cardControl);
        }

        // Show placeholders for undealt community cards
        for (int i = state.CommunityCards.Count; i < 5; i++)
        {
            var placeholder = CreateCardPlaceholder();
            DealerCardsPanel.Children.Add(placeholder);
        }

        DealerValueBadge.Visibility = Visibility.Collapsed;

        // Show pot
        PotDisplay.Visibility = Visibility.Visible;
        PotAmount.Text = $"${state.Pot:N0}";

        // Update other players display
        UpdateOtherPlayersDisplay(state);

        // Update current player's cards
        if (myPlayer != null)
        {
            UpdatePlayerCards(myPlayer.Hand, 0);
            
            // Show hand description if in showdown
            if (state.Phase == (int)GamePhase.Showdown || state.Phase == (int)GamePhase.GameOver)
            {
                if (!string.IsNullOrEmpty(myPlayer.HandDescription))
                {
                    PlayerValueBadge.Visibility = Visibility.Visible;
                    PlayerValueText.Text = myPlayer.HandDescription;
                }
            }
            else
            {
                PlayerValueBadge.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdateOtherPlayersDisplay(GameStateDto state)
    {
        OtherPlayersPanel.Children.Clear();
        
        var otherPlayers = state.Players.Where(p => p.ConnectionId != _hubService.ConnectionId).ToList();
        
        foreach (var player in otherPlayers)
        {
            var playerPanel = CreateOtherPlayerPanel(player, state);
            OtherPlayersPanel.Children.Add(playerPanel);
        }
    }

    private Border CreateOtherPlayerPanel(PlayerInfo player, GameStateDto state)
    {
        var isCurrentTurn = player.ConnectionId == state.CurrentPlayerId;
        var isShowdown = state.Phase == (int)GamePhase.Showdown || state.Phase == (int)GamePhase.GameOver;
        
        var panel = new Border
        {
            Background = isCurrentTurn ? new SolidColorBrush(Color.FromArgb(0x40, 0xD4, 0xAF, 0x37)) 
                                       : new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x00, 0x00)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            Margin = new Thickness(10, 0, 10, 0),
            MinWidth = 120
        };

        if (isCurrentTurn)
        {
            panel.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));
            panel.BorderThickness = new Thickness(2);
        }

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        // Player name
        var nameText = new TextBlock
        {
            Text = player.Username,
            Foreground = player.IsFolded ? Brushes.Gray : Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextDecorations = player.IsFolded ? TextDecorations.Strikethrough : null
        };
        stack.Children.Add(nameText);

        // Cards panel
        var cardsPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };

        if (player.Hand.Count > 0)
        {
            foreach (var card in player.Hand)
            {
                var cardVisual = CreateSmallCardVisual(card);
                cardsPanel.Children.Add(cardVisual);
            }
        }
        stack.Children.Add(cardsPanel);

        // Bet/Status info
        var statusText = "";
        if (player.IsFolded)
            statusText = "FOLDED";
        else if (player.IsAllIn)
            statusText = "ALL IN";
        else if (player.CurrentBet > 0)
            statusText = $"Bet: ${player.CurrentBet}";
        else if (player.TotalBetThisRound > 0)
            statusText = $"${player.TotalBetThisRound}";

        if (!string.IsNullOrEmpty(statusText))
        {
            var betText = new TextBlock
            {
                Text = statusText,
                Foreground = player.IsAllIn ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) 
                                            : new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            stack.Children.Add(betText);
        }

        // Hand description at showdown
        if (isShowdown && !player.IsFolded && !string.IsNullOrEmpty(player.HandDescription))
        {
            var handDesc = new TextBlock
            {
                Text = player.HandDescription,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            stack.Children.Add(handDesc);
        }

        // Balance
        var balanceText = new TextBlock
        {
            Text = $"${player.Balance:N0}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        stack.Children.Add(balanceText);

        panel.Child = stack;
        return panel;
    }

    private Border CreateSmallCardVisual(CardDto card)
    {
        var width = 35;
        var height = 50;
        
        var border = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(1, 0, 1, 0)
        };

        if (!card.IsFaceUp)
        {
            // Show card back
            border.Background = new LinearGradientBrush(
                Color.FromRgb(0x2C, 0x3E, 0x50),
                Color.FromRgb(0x1A, 0x25, 0x2F),
                45);
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));
            border.BorderThickness = new Thickness(1);
            
            var backText = new TextBlock
            {
                Text = "🂠",
                FontSize = 20,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = backText;
        }
        else
        {
            // Show card face
            border.Background = Brushes.White;
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            border.BorderThickness = new Thickness(1);

            var isRed = card.Suit == "Hearts" || card.Suit == "Diamonds";
            var foreground = isRed ? new SolidColorBrush(Color.FromRgb(220, 53, 69)) : new SolidColorBrush(Color.FromRgb(33, 37, 41));

            var suitSymbol = card.Suit switch
            {
                "Hearts" => "♥",
                "Diamonds" => "♦",
                "Clubs" => "♣",
                "Spades" => "♠",
                _ => ""
            };

            var valueDisplay = card.Value switch
            {
                "Ace" => "A",
                "King" => "K",
                "Queen" => "Q",
                "Jack" => "J",
                _ => card.Value
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            stack.Children.Add(new TextBlock
            {
                Text = valueDisplay,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = foreground,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock
            {
                Text = suitSymbol,
                FontSize = 10,
                Foreground = foreground,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            border.Child = stack;
        }

        return border;
    }

    private void UpdateBlackjackDisplay(GameStateDto state, PlayerInfo? myPlayer)
    {
        DealerLabel.Text = "DEALER";
        PotDisplay.Visibility = Visibility.Collapsed;

        // Update dealer hand
        UpdateDealerCards(state.DealerHand, state.DealerHandValue);

        // Update other players display
        UpdateOtherPlayersDisplay(state);

        // Update current player's cards
        if (myPlayer != null)
        {
            UpdatePlayerCards(myPlayer.Hand, myPlayer.HandValue);
        }
    }

    private Border CreateCardPlaceholder()
    {
        return new Border
        {
            Width = 60,
            Height = 85,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(3, 0, 3, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(2)
        };
    }

    private void UpdateDealerCards(List<CardDto> cards, int handValue)
    {
        DealerCardsPanel.Children.Clear();

        foreach (var card in cards)
        {
            var cardControl = CreateCardVisual(card, true);
            DealerCardsPanel.Children.Add(cardControl);
        }

        if (cards.Count > 0)
        {
            DealerValueBadge.Visibility = Visibility.Visible;
            DealerValueText.Text = cards.Any(c => !c.IsFaceUp) ? $"{handValue}+" : handValue.ToString();
        }
        else
        {
            DealerValueBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdatePlayerCards(List<CardDto> cards, int handValue)
    {
        PlayerCardsPanel.Children.Clear();

        foreach (var card in cards)
        {
            // Force show my own cards face-up (server might send them face-down for broadcast)
            var myCard = new CardDto 
            { 
                Suit = card.Suit, 
                Value = card.Value, 
                IsFaceUp = true  // Always show my cards face-up
            };
            var cardControl = CreateCardVisual(myCard, true);
            PlayerCardsPanel.Children.Add(cardControl);
        }

        var isPoker = _currentState?.IsPoker ?? false;
        if (cards.Count > 0 && !isPoker)
        {
            PlayerValueBadge.Visibility = Visibility.Visible;
            PlayerValueText.Text = handValue.ToString();
        }
        else if (!isPoker)
        {
            PlayerValueBadge.Visibility = Visibility.Collapsed;
        }
    }

    private Border CreateCardVisual(CardDto card, bool isLarge = false)
    {
        var width = isLarge ? 75 : 55;
        var height = isLarge ? 105 : 77;
        var fontSize = isLarge ? 22 : 16;
        var suitSize = isLarge ? 26 : 20;
        
        var border = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(4, 0, 4, 0)
        };
        
        // Add shadow effect
        border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 8,
            ShadowDepth = 2,
            Opacity = 0.4,
            Color = Colors.Black
        };

        if (!card.IsFaceUp)
        {
            // Card back design
            border.Background = new LinearGradientBrush(
                Color.FromRgb(30, 60, 114),
                Color.FromRgb(42, 82, 152),
                45);
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(212, 175, 55));
            border.BorderThickness = new Thickness(2);

            var backPattern = new Grid();
            backPattern.Children.Add(new TextBlock
            {
                Text = "🂠",
                FontSize = suitSize + 4,
                Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            border.Child = backPattern;
        }
        else
        {
            // Card face design
            border.Background = new LinearGradientBrush(
                Color.FromRgb(255, 255, 255),
                Color.FromRgb(245, 245, 245),
                90);
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            border.BorderThickness = new Thickness(1);

            var isRed = card.Suit == "Hearts" || card.Suit == "Diamonds";
            var foreground = isRed ? new SolidColorBrush(Color.FromRgb(220, 53, 69)) : new SolidColorBrush(Color.FromRgb(33, 37, 41));

            var suitSymbol = card.Suit switch
            {
                "Hearts" => "♥",
                "Diamonds" => "♦",
                "Clubs" => "♣",
                "Spades" => "♠",
                _ => ""
            };

            var valueDisplay = card.Value switch
            {
                "Ace" => "A",
                "King" => "K",
                "Queen" => "Q",
                "Jack" => "J",
                _ => card.Value
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            stack.Children.Add(new TextBlock
            {
                Text = valueDisplay,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = foreground,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock
            {
                Text = suitSymbol,
                FontSize = suitSize,
                Foreground = foreground,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            border.Child = stack;
        }

        // Add animation
        AnimateCardDeal(border);
        
        return border;
    }

    private void AnimateCardDeal(Border card)
    {
        card.RenderTransform = new TranslateTransform(0, -50);
        card.Opacity = 0;
        
        var translateAnim = new DoubleAnimation(-50, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        
        ((TranslateTransform)card.RenderTransform).BeginAnimation(TranslateTransform.YProperty, translateAnim);
        card.BeginAnimation(OpacityProperty, opacityAnim);
    }

    private void AnimateMessage()
    {
        GameMessage.RenderTransform = new ScaleTransform(1, 1);
        GameMessage.RenderTransformOrigin = new Point(0.5, 0.5);
        
        var scaleAnim = new DoubleAnimation(1.0, 1.1, TimeSpan.FromMilliseconds(150))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        ((ScaleTransform)GameMessage.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        ((ScaleTransform)GameMessage.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
    }

    private void AnimateChipAdd()
    {
        CurrentBetDisplay.RenderTransform = new ScaleTransform(1, 1);
        CurrentBetDisplay.RenderTransformOrigin = new Point(0.5, 0.5);
        
        var animation = new DoubleAnimation(1.1, 1.0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ((ScaleTransform)CurrentBetDisplay.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        ((ScaleTransform)CurrentBetDisplay.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private void UpdateControlsForPhase(GamePhase phase, bool isMyTurn, PlayerInfo? myPlayer, GameStateDto? state = null)
    {
        BettingPanel.Visibility = Visibility.Collapsed;
        ActionPanel.Visibility = Visibility.Collapsed;
        PokerActionPanel.Visibility = Visibility.Collapsed;
        WaitingMessage.Visibility = Visibility.Collapsed;
        NewRoundButton.Visibility = Visibility.Collapsed;
        RaisePanel.Visibility = Visibility.Collapsed;
        PlaceBetButton.Visibility = Visibility.Collapsed;
        ClearBetButton.Visibility = Visibility.Collapsed;
        MuckShowPanel.Visibility = Visibility.Collapsed;
        SpectatorPanel.Visibility = Visibility.Collapsed;
        StandUpButton.Visibility = Visibility.Collapsed;
        ToCallText.Text = ""; // Clear to-call text when not your turn

        var isPoker = state?.IsPoker ?? false;

        // Show spectator controls if watching
        if (_isSpectator)
        {
            SpectatorPanel.Visibility = Visibility.Visible;
            TakeSeatButton.IsEnabled = (state?.CurrentPlayerCount ?? 0) < 6; // Max 6 seats
            
            WaitingMessage.Text = "Watching the game...";
            WaitingMessage.Visibility = Visibility.Visible;
            return;
        }

        // Show stand up button when not in active hand
        if (phase == GamePhase.WaitingForPlayers || phase == GamePhase.GameOver)
        {
            StandUpButton.Visibility = Visibility.Visible;
        }

        switch (phase)
        {
            case GamePhase.WaitingForPlayers:
                WaitingMessage.Text = "Waiting for more players...";
                WaitingMessage.Visibility = Visibility.Visible;
                break;

            case GamePhase.Betting:
                if (myPlayer?.Status == (int)PlayerStatus.Waiting)
                {
                    if (isPoker)
                    {
                        // For Poker: just show Ready button (blinds are automatic)
                        BettingPanel.Visibility = Visibility.Visible;
                        PlaceBetButton.Visibility = Visibility.Visible;
                        PlaceBetButton.Content = "READY";
                        ClearBetButton.Visibility = Visibility.Collapsed;
                        ChipsPanel.Visibility = Visibility.Collapsed;
                        CurrentBetBorder.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // For Blackjack: show betting UI
                        BettingPanel.Visibility = Visibility.Visible;
                        PlaceBetButton.Visibility = Visibility.Visible;
                        PlaceBetButton.Content = "PLACE BET";
                        ClearBetButton.Visibility = Visibility.Visible;
                        ChipsPanel.Visibility = Visibility.Visible;
                        CurrentBetBorder.Visibility = Visibility.Visible;
                        UpdateChipButtons();
                    }
                }
                else
                {
                    WaitingMessage.Text = "Waiting for other players...";
                    WaitingMessage.Visibility = Visibility.Visible;
                }
                break;

            case GamePhase.PlayerTurn:
                // For poker, allow actions if it's my turn, I'm not folded, and I'm not all-in
                var canAct = isMyTurn && myPlayer != null && !myPlayer.IsFolded && !myPlayer.IsAllIn;
                
                if (canAct)
                {
                    if (isPoker)
                    {
                        ShowPokerActions(state!, myPlayer);
                        RaisePanel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Blackjack still uses the Playing status check
                        if (myPlayer.Status == (int)PlayerStatus.Playing)
                        {
                            ActionPanel.Visibility = Visibility.Visible;
                            HitButton.IsEnabled = true;
                            StandButton.IsEnabled = true;
                            AnimateMessage();
                        }
                        else
                        {
                            WaitingMessage.Text = "Your turn!";
                            WaitingMessage.Visibility = Visibility.Visible;
                        }
                    }
                }
                else
                {
                    WaitingMessage.Text = isMyTurn ? "Your turn!" : $"Waiting for {_currentState?.Message ?? "other player"}...";
                    WaitingMessage.Visibility = Visibility.Visible;
                }
                break;

            case GamePhase.DealerTurn:
                WaitingMessage.Text = isPoker ? "Showdown..." : "Dealer is playing...";
                WaitingMessage.Visibility = Visibility.Visible;
                break;

            case GamePhase.Showdown:
                // Show muck/show options if player hasn't folded and isn't the winner
                if (myPlayer != null && !myPlayer.IsFolded && myPlayer.Status != (int)PlayerStatus.Won)
                {
                    MuckShowPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    WaitingMessage.Text = "Showdown!";
                    WaitingMessage.Visibility = Visibility.Visible;
                }
                break;

            case GamePhase.GameOver:
                ShowResults(myPlayer);
                break;
        }
    }

    private void ShowResults(PlayerInfo? myPlayer)
    {
        if (myPlayer == null) return;

        var status = (PlayerStatus)myPlayer.Status;
        var isPoker = _currentState?.IsPoker ?? false;
        
        
        ResultEmoji.Text = status switch
        {
            PlayerStatus.Won or PlayerStatus.Blackjack => "🎉",
            PlayerStatus.Lost or PlayerStatus.Busted => "😢",
            PlayerStatus.Push => "🤝",
            _ => "🎮"
        };
        
        ResultTitle.Text = status switch
        {
            PlayerStatus.Won => "YOU WIN!",
            PlayerStatus.Blackjack => "BLACKJACK!",
            PlayerStatus.Lost => "YOU LOSE",
            PlayerStatus.Busted => "BUSTED!",
            PlayerStatus.Push => "PUSH",
            _ => "GAME OVER"
        };

        ResultTitle.Foreground = status switch
        {
            PlayerStatus.Won or PlayerStatus.Blackjack => Brushes.Gold,
            PlayerStatus.Lost or PlayerStatus.Busted => Brushes.IndianRed,
            _ => Brushes.White
        };

        if (isPoker)
        {
            var handDesc = myPlayer.HandDescription ?? "";
            ResultMessage.Text = status switch
            {
                PlayerStatus.Won => $"+${_currentState?.Pot ?? 0}\n{handDesc}",
                _ => $"{handDesc}"
            };
            ResultMessage.Foreground = status == PlayerStatus.Won ? Brushes.LightGreen : Brushes.IndianRed;
        }
        else
        {
            ResultMessage.Text = status switch
            {
                PlayerStatus.Won => $"+${myPlayer.CurrentBet * 2}",
                PlayerStatus.Blackjack => $"+${(int)(myPlayer.CurrentBet * 2.5)}",
                PlayerStatus.Push => $"${myPlayer.CurrentBet} returned",
                _ => $"-${myPlayer.CurrentBet}"
            };
            ResultMessage.Foreground = status switch
            {
                PlayerStatus.Won or PlayerStatus.Blackjack => Brushes.LightGreen,
                PlayerStatus.Push => Brushes.White,
                _ => Brushes.IndianRed
            };
        }

        ResultOverlay.Visibility = Visibility.Visible;
        AnimateResultOverlay();
    }

    private void AnimateResultOverlay()
    {
        ResultOverlay.Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        ResultOverlay.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void UpdateUI()
    {
        BalanceDisplay.Text = _currentUser.FormattedBalance;
        CurrentBetDisplay.Text = $"${_currentBet:N0}";
        UpdateChipButtons();
    }

    private void UpdateChipButtons()
    {
        Chip10.IsEnabled = _currentUser.Balance >= 10;
        Chip25.IsEnabled = _currentUser.Balance >= 25;
        Chip50.IsEnabled = _currentUser.Balance >= 50;
        Chip100.IsEnabled = _currentUser.Balance >= 100;
    }

    private void ChipButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagValue && int.TryParse(tagValue, out int chipValue))
        {
            if (_currentBet + chipValue <= _currentUser.Balance)
            {
                _currentBet += chipValue;
                CurrentBetDisplay.Text = $"${_currentBet:N0}";
                PlaceBetButton.IsEnabled = _currentBet > 0;
                AnimateChipAdd();
            }
        }
    }

    private void ClearBetButton_Click(object sender, RoutedEventArgs e)
    {
        _currentBet = 0;
        CurrentBetDisplay.Text = "$0";
        PlaceBetButton.IsEnabled = false;
    }

    private async void PlaceBetButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check if this is Poker (Ready button) or Blackjack (Bet button)
            if (_currentState?.IsPoker == true)
            {
                // Poker: just mark as ready
                await _hubService.SetReadyAsync(_roomId);
            }
            else
            {
                // Blackjack: place bet
                if (_currentBet <= 0) return;
                await _hubService.PlaceBetAsync(_roomId, _currentBet);
            }
            
            BettingPanel.Visibility = Visibility.Collapsed;
            WaitingMessage.Text = "Waiting for other players...";
            WaitingMessage.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void HitButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HitButton.IsEnabled = false;
            StandButton.IsEnabled = false;
            await _hubService.HitAsync(_roomId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            HitButton.IsEnabled = true;
            StandButton.IsEnabled = true;
        }
    }

    private async void StandButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HitButton.IsEnabled = false;
            StandButton.IsEnabled = false;
            await _hubService.StandAsync(_roomId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            HitButton.IsEnabled = true;
            StandButton.IsEnabled = true;
        }
    }

    private async void NewRoundButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _hubService.NewRoundAsync(_roomId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region Poker Actions

    private void ShowPokerActions(GameStateDto state, PlayerInfo myPlayer)
    {
        PokerActionPanel.Visibility = Visibility.Visible;

        var toCall = state.CurrentBetToMatch - myPlayer.TotalBetThisRound;
        ToCallText.Text = toCall > 0 ? $"${toCall} to call" : "Check or raise";

        // Enable/disable buttons based on state
        CheckButton.IsEnabled = toCall == 0;
        CheckButton.Visibility = toCall == 0 ? Visibility.Visible : Visibility.Collapsed;
        
        CallButton.IsEnabled = toCall > 0 && myPlayer.Balance >= toCall;
        CallButton.Visibility = toCall > 0 ? Visibility.Visible : Visibility.Collapsed;
        CallButton.Content = toCall > 0 ? $"CALL ${toCall}" : "CALL";

        RaiseButton.IsEnabled = myPlayer.Balance > toCall;
        RaiseAmountBox.Text = Math.Max(state.MinRaise, state.CurrentBetToMatch).ToString();

        AllInButton.IsEnabled = myPlayer.Balance > 0;
    }

    private async void FoldButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DisablePokerButtons();
            await _hubService.PokerFoldAsync(_roomId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            EnablePokerButtons();
        }
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DisablePokerButtons();
            await _hubService.PokerCheckAsync(_roomId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            EnablePokerButtons();
        }
    }

    private async void CallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DisablePokerButtons();
            await _hubService.PokerCallAsync(_roomId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            EnablePokerButtons();
        }
    }

    private async void RaiseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(RaiseAmountBox.Text, out int raiseAmount) || raiseAmount <= 0)
            {
                MessageBox.Show("Please enter a valid raise amount.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DisablePokerButtons();
            await _hubService.PokerRaiseAsync(_roomId, raiseAmount);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            EnablePokerButtons();
        }
    }

    private async void AllInButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DisablePokerButtons();
            await _hubService.PokerAllInAsync(_roomId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            EnablePokerButtons();
        }
    }

    private void DisablePokerButtons()
    {
        FoldButton.IsEnabled = false;
        CheckButton.IsEnabled = false;
        CallButton.IsEnabled = false;
        RaiseButton.IsEnabled = false;
        AllInButton.IsEnabled = false;
    }

    private void EnablePokerButtons()
    {
        FoldButton.IsEnabled = true;
        CheckButton.IsEnabled = true;
        CallButton.IsEnabled = true;
        RaiseButton.IsEnabled = true;
        AllInButton.IsEnabled = true;
    }

    #endregion

    #region Spectator & Muck/Show Actions

    private async void TakeSeatButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var seats = await _hubService.GetAvailableSeatsAsync(_roomId);
            if (seats.Count > 0)
            {
                // Take the first available seat
                var success = await _hubService.TakeSeatAsync(_roomId, seats[0]);
                if (success)
                {
                    _isSpectator = false;
                    SpectatorPanel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                MessageBox.Show("No seats available!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void StandUpButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var success = await _hubService.StandUpAsync(_roomId);
            if (success)
            {
                _isSpectator = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MuckButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MuckButton.IsEnabled = false;
            ShowButton.IsEnabled = false;
            await _hubService.MuckCardsAsync(_roomId);
            MuckShowPanel.Visibility = Visibility.Collapsed;
            
            // Show message
            GameMessage.Text = "Cards mucked";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            MuckButton.IsEnabled = true;
            ShowButton.IsEnabled = true;
        }
    }

    private async void ShowButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MuckButton.IsEnabled = false;
            ShowButton.IsEnabled = false;
            await _hubService.ShowCardsAsync(_roomId);
            MuckShowPanel.Visibility = Visibility.Collapsed;
            
            GameMessage.Text = "Cards shown";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            MuckButton.IsEnabled = true;
            ShowButton.IsEnabled = true;
        }
    }

    #endregion

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        ResultOverlay.Visibility = Visibility.Collapsed;
        NewRoundButton.Visibility = Visibility.Visible;
    }

    private async void LeaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _hubService.LeaveRoomAsync(_roomId);
            UserUpdated?.Invoke(this, _currentUser);
            LeaveRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error leaving room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Cleanup()
    {
        _turnTimer?.Stop();
        _hubService.OnGameStateUpdated -= OnGameStateUpdated;
        _hubService.OnGameStarted -= OnGameStarted;
        _hubService.OnDealerTurnComplete -= OnDealerTurnComplete;
        _hubService.OnNewRoundStarted -= OnNewRoundStarted;
        _hubService.OnPlayerLeft -= OnPlayerLeft;
        _hubService.OnTurnTimeout -= OnTurnTimeout;
        _hubService.OnTurnTimerStarted -= OnTurnTimerStarted;
    }
}
