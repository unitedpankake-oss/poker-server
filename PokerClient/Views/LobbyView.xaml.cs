using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BlackjackGame.Models;
using BlackjackGame.Services;

namespace BlackjackGame.Views;

public partial class LobbyView : UserControl
{
    private GameHubService _hubService = null!;
    private UserAccount _currentUser = null!;
    private List<RoomInfoDto> _rooms = [];
    private bool _isOnline;

    public event EventHandler? BackRequested;
    public event EventHandler? SinglePlayerRequested;
    public event EventHandler<(string roomId, GameHubService hubService, bool asSpectator)>? RoomJoined;

    public LobbyView()
    {
        InitializeComponent();
    }

    private void SetupEventHandlers()
    {
        _hubService.OnConnected += (connectionId) =>
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionStatus.Text = $"Connected (ID: {connectionId[..8]}...)";
                ConnectButton.Content = "DISCONNECT";
                CreateRoomButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
                _ = LoadRoomsAsync();
            });
        };

        _hubService.OnDisconnected += (error) =>
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionStatus.Text = "Disconnected";
                ConnectButton.Content = "CONNECT";
                CreateRoomButton.IsEnabled = false;
                RefreshButton.IsEnabled = false;
                RoomsList.ItemsSource = null;
            });
        };

        _hubService.OnRoomDeleted += (roomId) =>
        {
            Dispatcher.Invoke(() => _ = LoadRoomsAsync());
        };
    }

    public void Initialize(UserAccount user, GameHubService hubService, bool isOnline)
    {
        _currentUser = user;
        _hubService = hubService;
        _isOnline = isOnline;
        
        UsernameDisplay.Text = user.Username;
        BalanceDisplay.Text = user.FormattedBalance;
        
        SetupEventHandlers();
        
        if (isOnline && hubService.IsConnected)
        {
            // Already connected from login
            ConnectionStatus.Text = $"Connected (ID: {hubService.ConnectionId[..Math.Min(8, hubService.ConnectionId.Length)]}...)";
            ConnectButton.Content = "DISCONNECT";
            CreateRoomButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
            ServerAddressBox.IsEnabled = false;
            _ = LoadRoomsAsync();
        }
        else
        {
            ConnectionStatus.Text = "Offline Mode";
            ConnectButton.IsEnabled = false;
            CreateRoomButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hubService.IsConnected)
        {
            await _hubService.DisconnectAsync();
        }
        else
        {
            try
            {
                ConnectionStatus.Text = "Connecting...";
                ConnectButton.IsEnabled = false;
                await _hubService.ConnectAsync(ServerAddressBox.Text);
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"Failed: {ex.Message}";
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadRoomsAsync();
    }

    private async Task LoadRoomsAsync()
    {
        try
        {
            _rooms = await _hubService.GetRoomsAsync();
            RoomsList.ItemsSource = _rooms;
            NoRoomsMessage.Visibility = _rooms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load rooms: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RoomItem_Click(object sender, MouseButtonEventArgs e)
    {
        // Room selection visual feedback
    }

    private async void JoinRoom_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string roomId)
        {
            await JoinRoomAsync(roomId, asSpectator: false);
        }
    }

    private async void WatchRoom_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string roomId)
        {
            await JoinRoomAsync(roomId, asSpectator: true);
        }
    }

    private async void DeleteRoom_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string roomId)
        {
            var result = MessageBox.Show("Are you sure you want to delete this room?", "Confirm Delete", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var success = await _hubService.DeleteRoomAsync(roomId);
                    if (!success)
                    {
                        // Try force delete if room has players
                        var forceResult = MessageBox.Show("Room has players. Force delete?", "Force Delete", 
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (forceResult == MessageBoxResult.Yes)
                        {
                            await _hubService.ForceDeleteRoomAsync(roomId);
                        }
                    }
                    await LoadRoomsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async Task JoinRoomAsync(string roomId, bool asSpectator)
    {
        try
        {
            var success = await _hubService.JoinRoomAsync(roomId, _currentUser.Username, _currentUser.Balance, asSpectator);
            if (success)
            {
                RoomJoined?.Invoke(this, (roomId, _hubService, asSpectator));
            }
            else
            {
                MessageBox.Show("Failed to join room.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error joining room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateRoomButton_Click(object sender, RoutedEventArgs e)
    {
        NewRoomNameBox.Text = $"{_currentUser.Username}'s Room";
        CreateRoomDialog.Visibility = Visibility.Visible;
    }

    private async void ConfirmCreateRoom_Click(object sender, RoutedEventArgs e)
    {
        var roomName = NewRoomNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(roomName))
        {
            MessageBox.Show("Please enter a room name.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var minBet = MinBetCombo.SelectedIndex switch
        {
            0 => 10,
            1 => 25,
            2 => 50,
            3 => 100,
            _ => 25
        };

        // 0 = Blackjack, 1 = Poker
        var gameMode = PokerMode.IsChecked == true ? 1 : 0;

        try
        {
            var room = await _hubService.CreateRoomAsync(roomName, gameMode, minBet);
            if (room != null)
            {
                CreateRoomDialog.Visibility = Visibility.Collapsed;
                await JoinRoomAsync(room.RoomId, asSpectator: false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelCreateRoom_Click(object sender, RoutedEventArgs e)
    {
        CreateRoomDialog.Visibility = Visibility.Collapsed;
    }

    private void SinglePlayerButton_Click(object sender, RoutedEventArgs e)
    {
        SinglePlayerRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e)
    {
        await _hubService.DisconnectAsync();
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
