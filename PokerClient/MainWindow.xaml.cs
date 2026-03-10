using System.Windows;
using System.Windows.Media.Animation;
using BlackjackGame.Models;
using BlackjackGame.Services;

namespace BlackjackGame
{
    public partial class MainWindow : Window
    {
        private UserAccount? _currentUser;
        private GameHubService? _currentHubService;
        private bool _isOnline;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginView_LoginSuccessful(object? sender, (UserAccount User, GameHubService HubService, bool IsOnline) args)
        {
            _currentUser = args.User;
            _currentHubService = args.HubService;
            _isOnline = args.IsOnline;
            
            // Animate transition to lobby
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) =>
            {
                LoginViewControl.Visibility = Visibility.Collapsed;
                LobbyViewControl.Initialize(args.User, args.HubService, args.IsOnline);
                LobbyViewControl.Visibility = Visibility.Visible;
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                LobbyViewControl.BeginAnimation(OpacityProperty, fadeIn);
            };
            LoginViewControl.BeginAnimation(OpacityProperty, fadeOut);
        }

        private async void LobbyView_BackRequested(object? sender, EventArgs e)
        {
            // Disconnect from server if online
            if (_isOnline && _currentHubService != null)
            {
                await _currentHubService.DisconnectAsync();
            }

            // Animate transition back to login
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, args) =>
            {
                LobbyViewControl.Visibility = Visibility.Collapsed;
                LoginViewControl.Visibility = Visibility.Visible;
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                LoginViewControl.BeginAnimation(OpacityProperty, fadeIn);
            };
            LobbyViewControl.BeginAnimation(OpacityProperty, fadeOut);
            
            _currentUser = null;
            _currentHubService = null;
            _isOnline = false;
        }

        private void LobbyView_RoomJoined(object? sender, (string roomId, GameHubService hubService, bool asSpectator) args)
        {
            if (_currentUser == null) return;

            _currentHubService = args.hubService;

            // Animate transition to multiplayer game
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) =>
            {
                LobbyViewControl.Visibility = Visibility.Collapsed;
                MultiplayerGameViewControl.Initialize(_currentUser, args.roomId, args.hubService, args.asSpectator);
                MultiplayerGameViewControl.Visibility = Visibility.Visible;
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                MultiplayerGameViewControl.BeginAnimation(OpacityProperty, fadeIn);
            };
            LobbyViewControl.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void LobbyView_SinglePlayerRequested(object? sender, EventArgs e)
        {
            if (_currentUser == null) return;

            // Animate transition to single player game
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, args) =>
            {
                LobbyViewControl.Visibility = Visibility.Collapsed;
                GameTableViewControl.Initialize(_currentUser);
                GameTableViewControl.Visibility = Visibility.Visible;
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                GameTableViewControl.BeginAnimation(OpacityProperty, fadeIn);
            };
            LobbyViewControl.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void MultiplayerGameView_LeaveRequested(object? sender, EventArgs e)
        {
            MultiplayerGameViewControl.Cleanup();

            // Animate transition back to lobby
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, args) =>
            {
                MultiplayerGameViewControl.Visibility = Visibility.Collapsed;
                if (_currentUser != null && _currentHubService != null)
                {
                    LobbyViewControl.Initialize(_currentUser, _currentHubService, _isOnline);
                }
                LobbyViewControl.Visibility = Visibility.Visible;
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                LobbyViewControl.BeginAnimation(OpacityProperty, fadeIn);
            };
            MultiplayerGameViewControl.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void MultiplayerGameView_UserUpdated(object? sender, UserAccount user)
        {
            _currentUser = user;
            // Balance updates are synced via server
        }

        private void GameTableView_LogoutRequested(object? sender, EventArgs e)
        {
            // Animate transition back to lobby
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, args) =>
            {
                GameTableViewControl.Visibility = Visibility.Collapsed;
                if (_currentUser != null && _currentHubService != null)
                {
                    LobbyViewControl.Initialize(_currentUser, _currentHubService, _isOnline);
                }
                LobbyViewControl.Visibility = Visibility.Visible;
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                LobbyViewControl.BeginAnimation(OpacityProperty, fadeIn);
            };
            GameTableViewControl.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void GameTableView_UserUpdated(object? sender, UserAccount user)
        {
            _currentUser = user;
            // For online users, sync balance to server
            if (_isOnline && _currentHubService != null && !user.Username.StartsWith("Guest_"))
            {
                _ = _currentHubService.UpdateUserBalanceAsync(user.Id, user.Balance);
            }
        }
    }
}