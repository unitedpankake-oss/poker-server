using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BlackjackGame.Models;
using BlackjackGame.Services;

namespace BlackjackGame.Views
{
    public partial class LoginView : UserControl
    {
        private readonly GameHubService _hubService = new();
        private bool _isConnected;

        public event EventHandler<(UserAccount User, GameHubService HubService, bool IsOnline)>? LoginSuccessful;

        public LoginView()
        {
            InitializeComponent();
            _ = AutoConnectAsync();
        }

        private async Task AutoConnectAsync()
        {
            // Try to auto-connect on startup
            await Task.Delay(500); // Small delay to let UI render
            await ConnectToServerAsync();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectToServerAsync();
        }

        private async Task ConnectToServerAsync()
        {
            if (_isConnected)
            {
                // Disconnect
                await _hubService.DisconnectAsync();
                _isConnected = false;
                UpdateConnectionUI(false);
                return;
            }

            try
            {
                ConnectionStatus.Text = "Connecting...";
                ConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));
                ConnectButton.IsEnabled = false;
                
                await _hubService.ConnectAsync(ServerUrlBox.Text);
                
                _isConnected = true;
                UpdateConnectionUI(true);
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"Failed: {ex.Message}";
                ConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                _isConnected = false;
                UpdateConnectionUI(false);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        private void UpdateConnectionUI(bool connected)
        {
            if (connected)
            {
                ConnectionStatus.Text = "✓ Connected to server";
                ConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
                ConnectButton.Content = "Disconnect";
                AuthPanel.IsEnabled = true;
                AuthPanel.Opacity = 1;
            }
            else
            {
                ConnectionStatus.Text = "Not connected";
                ConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
                ConnectButton.Content = "Connect";
                AuthPanel.IsEnabled = false;
                AuthPanel.Opacity = 0.5;
            }
        }

        private void LoginTab_Checked(object sender, RoutedEventArgs e)
        {
            if (LoginForm != null && RegisterForm != null)
            {
                LoginForm.Visibility = Visibility.Visible;
                RegisterForm.Visibility = Visibility.Collapsed;
                ErrorMessage.Text = string.Empty;
            }
        }

        private void RegisterTab_Checked(object sender, RoutedEventArgs e)
        {
            if (LoginForm != null && RegisterForm != null)
            {
                LoginForm.Visibility = Visibility.Collapsed;
                RegisterForm.Visibility = Visibility.Visible;
                ErrorMessage.Text = string.Empty;
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = LoginUsernameBox.Text.Trim();
            var password = LoginPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage.Text = "Please enter username and password";
                return;
            }

            if (!_isConnected)
            {
                ErrorMessage.Text = "Please connect to server first";
                return;
            }

            try
            {
                ErrorMessage.Text = "Signing in...";
                var userDto = await _hubService.LoginAsync(username, password);
                
                if (userDto != null)
                {
                    var user = ConvertToUserAccount(userDto);
                    LoginSuccessful?.Invoke(this, (user, _hubService, true));
                }
                else
                {
                    ErrorMessage.Text = "Invalid username or password";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = $"Login error: {ex.Message}";
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var username = RegisterUsernameBox.Text.Trim();
            var email = RegisterEmailBox.Text.Trim();
            var password = RegisterPasswordBox.Password;
            var confirmPassword = RegisterConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || 
                string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage.Text = "Please fill in all fields";
                return;
            }

            if (password != confirmPassword)
            {
                ErrorMessage.Text = "Passwords do not match";
                return;
            }

            if (password.Length < 6)
            {
                ErrorMessage.Text = "Password must be at least 6 characters";
                return;
            }

            if (!_isConnected)
            {
                ErrorMessage.Text = "Please connect to server first";
                return;
            }

            try
            {
                ErrorMessage.Text = "Creating account...";
                var result = await _hubService.RegisterAsync(username, email, password);
                
                if (result.Success && result.User != null)
                {
                    var user = ConvertToUserAccount(result.User);
                    LoginSuccessful?.Invoke(this, (user, _hubService, true));
                }
                else
                {
                    ErrorMessage.Text = result.Message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = $"Registration error: {ex.Message}";
            }
        }

        private void GuestButton_Click(object sender, RoutedEventArgs e)
        {
            var guestUser = new UserAccount
            {
                Username = $"Guest_{Random.Shared.Next(1000, 9999)}",
                Balance = 500
            };
            // Guest plays offline - no hub service
            LoginSuccessful?.Invoke(this, (guestUser, _hubService, false));
        }

        private static UserAccount ConvertToUserAccount(UserDto dto)
        {
            return new UserAccount
            {
                Id = dto.Id,
                Username = dto.Username,
                Email = dto.Email,
                Balance = dto.Balance,
                AvatarUrl = dto.AvatarUrl,
                CreatedAt = dto.CreatedAt,
                LastLogin = dto.LastLogin,
                GamesPlayed = dto.GamesPlayed,
                GamesWon = dto.GamesWon,
                TotalWinnings = dto.TotalWinnings
            };
        }
    }
}
