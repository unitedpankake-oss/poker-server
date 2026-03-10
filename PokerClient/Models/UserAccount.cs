using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BlackjackGame.Models
{
    public class UserAccount : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _passwordHash = string.Empty;
        private int _balance = 1000;
        private string _avatarUrl = "default";
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime _lastLogin = DateTime.UtcNow;
        private int _gamesPlayed;
        private int _gamesWon;
        private int _totalWinnings;

        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
        public string PasswordHash { get => _passwordHash; set { _passwordHash = value; OnPropertyChanged(); } }
        public int Balance { get => _balance; set { _balance = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedBalance)); } }
        public string FormattedBalance => $"${Balance:N0}";
        public string AvatarUrl { get => _avatarUrl; set { _avatarUrl = value; OnPropertyChanged(); } }
        public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
        public DateTime LastLogin { get => _lastLogin; set { _lastLogin = value; OnPropertyChanged(); } }
        public int GamesPlayed { get => _gamesPlayed; set { _gamesPlayed = value; OnPropertyChanged(); } }
        public int GamesWon { get => _gamesWon; set { _gamesWon = value; OnPropertyChanged(); OnPropertyChanged(nameof(WinRate)); } }
        public int TotalWinnings { get => _totalWinnings; set { _totalWinnings = value; OnPropertyChanged(); } }

        public double WinRate => GamesPlayed > 0 ? (double)GamesWon / GamesPlayed * 100 : 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class UserDataStore
    {
        private const string DataFileName = "users.json";
        private List<UserAccount> _users = [];

        public UserDataStore()
        {
            Load();
        }

        public void Load()
        {
            if (File.Exists(DataFileName))
            {
                var json = File.ReadAllText(DataFileName);
                _users = JsonSerializer.Deserialize<List<UserAccount>>(json) ?? [];
            }
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DataFileName, json);
        }

        public UserAccount? Authenticate(string username, string password)
        {
            var passwordHash = HashPassword(password);
            return _users.FirstOrDefault(u => 
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && 
                u.PasswordHash == passwordHash);
        }

        public (bool Success, string Message) Register(string username, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                return (false, "Username must be at least 3 characters");

            if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return (false, "Username already exists");

            if (_users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return (false, "Email already registered");

            var user = new UserAccount
            {
                Username = username,
                Email = email,
                PasswordHash = HashPassword(password),
                Balance = 1000
            };

            _users.Add(user);
            Save();
            return (true, "Registration successful!");
        }

        public void UpdateUser(UserAccount user)
        {
            var existing = _users.FirstOrDefault(u => u.Id == user.Id);
            if (existing != null)
            {
                var index = _users.IndexOf(existing);
                _users[index] = user;
                Save();
            }
        }

        public void AddBonus(UserAccount user, int amount)
        {
            user.Balance += amount;
            UpdateUser(user);
        }

        private static string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password + "casino_salt_2024");
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
