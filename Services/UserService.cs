using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PokerServer.Services;

public class UserService
{
    private const string UsersFileName = "users.json";
    private const string AdminPassword = "IvanHovorun18!"; // Change this!  
    private readonly object _lock = new();
    private List<UserAccount> _users = [];

    public UserService()
    {
        LoadUsers();
    }

    private void LoadUsers()
    {
        try
        {
            if (File.Exists(UsersFileName))
            {
                var json = File.ReadAllText(UsersFileName);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    _users = JsonSerializer.Deserialize<List<UserAccount>>(json) ?? [];
                    Console.WriteLine($"Loaded {_users.Count} users from {UsersFileName}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading users: {ex.Message}");
            _users = [];
        }
    }

    private void SaveUsers()
    {
        try
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(UsersFileName, json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving users: {ex.Message}");
        }
    }

    public UserAccount? Authenticate(string username, string password)
    {
        lock (_lock)
        {
            var passwordHash = HashPassword(password);
            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.PasswordHash == passwordHash);

            if (user != null)
            {
                if (user.IsBanned)
                    return null; // banned users cannot log in
                user.LastLogin = DateTime.UtcNow;
                SaveUsers();
                AddActivity($"User logged in: {user.Username}");
            }

            return user;
        }
    }

    public (bool Success, string Message, UserAccount? User) Register(string username, string email, string password)
    {
        Console.WriteLine($"Register called: username={username}, email={email}");
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                return (false, "Username must be at least 3 characters", null);

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return (false, "Password must be at least 6 characters", null);

            if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return (false, "Username already exists", null);

            if (!string.IsNullOrWhiteSpace(email) && _users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return (false, "Email already registered", null);

            var user = new UserAccount
            {
                Username = username,
                Email = email,
                PasswordHash = HashPassword(password),
                Balance = 1000
            };

            _users.Add(user);
            Console.WriteLine($"User registered: {username}, total users: {_users.Count}");
            SaveUsers();
            AddActivity($"New user registered: {username} ({email})");
            return (true, "Registration successful!", user);
        }
    }

    public bool ValidateAdminPassword(string password)
    {
        var isValid = password == AdminPassword;
        Console.WriteLine($"ValidateAdminPassword: input='{password}', expected='{AdminPassword}', valid={isValid}");
        return isValid;
    }

    public string GetAdminPassword() => AdminPassword;

    public List<UserAccount> GetAllUsers(string adminPassword)
    {
        Console.WriteLine($"GetAllUsers called. Total users in memory: {_users.Count}");
        if (!ValidateAdminPassword(adminPassword))
        {
            Console.WriteLine("GetAllUsers: Invalid admin password");
            return [];
        }

        lock (_lock)
        {
            Console.WriteLine($"GetAllUsers: Returning {_users.Count} users");
            return _users.ToList();
        }
    }

    public UserAccount? GetUser(string id)
    {
        lock (_lock)
        {
            return _users.FirstOrDefault(u => u.Id == id);
        }
    }

    public bool UpdateUser(string adminPassword, UserAccount updatedUser)
    {
        if (!ValidateAdminPassword(adminPassword))
            return false;

        lock (_lock)
        {
            var existing = _users.FirstOrDefault(u => u.Id == updatedUser.Id);
            if (existing == null)
                return false;

            existing.Username = updatedUser.Username;
            existing.Email = updatedUser.Email;
            existing.Balance = updatedUser.Balance;
            existing.GamesPlayed = updatedUser.GamesPlayed;
            existing.GamesWon = updatedUser.GamesWon;
            existing.TotalWinnings = updatedUser.TotalWinnings;
            existing.AvatarUrl = updatedUser.AvatarUrl;

            if (!string.IsNullOrWhiteSpace(updatedUser.PasswordHash) && updatedUser.PasswordHash != existing.PasswordHash)
            {
                existing.PasswordHash = updatedUser.PasswordHash;
            }

            SaveUsers();
            return true;
        }
    }

    public bool SetPassword(string adminPassword, string userId, string newPassword)
    {
        if (!ValidateAdminPassword(adminPassword))
            return false;

        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return false;

            user.PasswordHash = HashPassword(newPassword);
            SaveUsers();
            return true;
        }
    }

    public bool DeleteUser(string adminPassword, string userId)
    {
        if (!ValidateAdminPassword(adminPassword))
            return false;

        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return false;

            _users.Remove(user);
            SaveUsers();
            return true;
        }
    }

    public bool AddBalance(string userId, int amount)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return false;

            user.Balance += amount;
            SaveUsers();
            return true;
        }
    }

    public bool UpdateBalance(string userId, int newBalance)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return false;

            user.Balance = newBalance;
            SaveUsers();
            return true;
        }
    }

    public bool UpdateStats(string userId, int gamesPlayed, int gamesWon, int totalWinnings)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return false;

            user.GamesPlayed = gamesPlayed;
            user.GamesWon = gamesWon;
            user.TotalWinnings = totalWinnings;
            SaveUsers();
            return true;
        }
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "casino_salt_2024");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public bool BanUser(string adminPassword, string userId, string? reason)
    {
        if (!ValidateAdminPassword(adminPassword)) return false;
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;
            user.IsBanned = true;
            user.BanReason = reason;
            SaveUsers();
            AddActivity($"User '{user.Username}' was BANNED. Reason: {reason ?? "none"}");
            return true;
        }
    }

    public bool UnbanUser(string adminPassword, string userId)
    {
        if (!ValidateAdminPassword(adminPassword)) return false;
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;
            user.IsBanned = false;
            user.BanReason = null;
            SaveUsers();
            AddActivity($"User '{user.Username}' was UNBANNED");
            return true;
        }
    }

    // --- Activity Log ---
    private readonly List<ActivityEntry> _activityLog = [];
    private readonly object _activityLock = new();

    public void AddActivity(string message)
    {
        lock (_activityLock)
        {
            _activityLog.Add(new ActivityEntry { Message = message });
            if (_activityLog.Count > 200)
                _activityLog.RemoveAt(0);
        }
    }

    public List<ActivityEntry> GetRecentActivity(string adminPassword, int count = 50)
    {
        if (!ValidateAdminPassword(adminPassword)) return [];
        lock (_activityLock)
        {
            return _activityLog.OrderByDescending(a => a.Timestamp).Take(count).ToList();
        }
    }

    public static string CreatePasswordHash(string password) => HashPassword(password);
}

public class ActivityEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public UserDto? User { get; set; }
}

public class RegisterResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public UserDto? User { get; set; }
}

public class UserAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int Balance { get; set; } = 1000;
    public string AvatarUrl { get; set; } = "default";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int TotalWinnings { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Balance { get; set; }
    public string AvatarUrl { get; set; } = "default";
    public DateTime CreatedAt { get; set; }
    public DateTime LastLogin { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int TotalWinnings { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }

    public static UserDto FromAccount(UserAccount account) => new()
    {
        Id = account.Id,
        Username = account.Username,
        Email = account.Email,
        Balance = account.Balance,
        AvatarUrl = account.AvatarUrl,
        CreatedAt = account.CreatedAt,
        LastLogin = account.LastLogin,
        GamesPlayed = account.GamesPlayed,
        GamesWon = account.GamesWon,
        TotalWinnings = account.TotalWinnings,
        IsBanned = account.IsBanned,
        BanReason = account.BanReason
    };
}
