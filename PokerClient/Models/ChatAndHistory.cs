using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlackjackGame.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _senderId = string.Empty;
        private string _senderName = string.Empty;
        private string _message = string.Empty;
        private DateTime _timestamp = DateTime.Now;

        public string SenderId { get => _senderId; set { _senderId = value; OnPropertyChanged(); } }
        public string SenderName { get => _senderName; set { _senderName = value; OnPropertyChanged(); } }
        public string Message { get => _message; set { _message = value; OnPropertyChanged(); } }
        public DateTime Timestamp { get => _timestamp; set { _timestamp = value; OnPropertyChanged(); } }
        public string TimeString => Timestamp.ToString("HH:mm");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class GameHistoryEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public GameMode Mode { get; set; }
        public string Result { get; set; } = string.Empty;
        public int BetAmount { get; set; }
        public int WinAmount { get; set; }
        public List<string> PlayerCards { get; set; } = [];
        public List<string> DealerCards { get; set; } = [];
        public int PlayerHandValue { get; set; }
        public int DealerHandValue { get; set; }

        public string ResultSummary => WinAmount > 0 
            ? $"+${WinAmount:N0}" 
            : WinAmount < 0 
                ? $"-${Math.Abs(WinAmount):N0}" 
                : "Push";

        public string TimeString => Timestamp.ToString("HH:mm");
    }
}
