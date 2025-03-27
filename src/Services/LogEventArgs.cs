using System;
using FlashSync.Core;

namespace FlashSync.Services
{
    public class LogEventArgs : EventArgs
    {
        public string Message { get; }
        public LogLevel Level { get; }
        public DateTime Timestamp { get; }
        public LogEntry LogEntry { get; }
        
        public LogEventArgs(string message, LogLevel level, DateTime timestamp, LogEntry logEntry = null)
        {
            Message = message;
            Level = level;
            Timestamp = timestamp;
            LogEntry = logEntry;
        }
        
        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {Message}";
        }
    }
} 