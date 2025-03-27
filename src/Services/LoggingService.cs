using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlashSync.Core;

namespace FlashSync.Services
{
    public class LoggingService
    {
        private readonly string _logFilePath;
        private readonly List<LogEntry> _logs = new List<LogEntry>();
        private const int MaxLogsToKeep = 1000;
        
        public event EventHandler<LogEventArgs> LogAdded;
        
        public LoggingService()
        {
            // Initialize logs list
            _logs = new List<LogEntry>();
            
            // Set log file path
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "File-Sync");
            
            _logFilePath = Path.Combine(appDataPath, "logs.txt");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            // Load existing logs
            LoadLogs();
        }
        
        private void LoadLogs()
        {
            if (!File.Exists(_logFilePath))
                return;
            
            try
            {
                string[] lines = File.ReadAllLines(_logFilePath);
                foreach (var line in lines)
                {
                    if (TryParseLogEntry(line, out LogEntry entry))
                    {
                        _logs.Add(entry);
                    }
                }
            }
            catch (Exception)
            {
                // Failed to load logs, start fresh
                _logs.Clear();
            }
        }
        
        private bool TryParseLogEntry(string line, out LogEntry entry)
        {
            entry = null;
            
            try
            {
                // Format: [yyyy-MM-dd HH:mm:ss] [Level] Message
                int dateEndIndex = line.IndexOf(']');
                if (dateEndIndex < 0) return false;
                
                string dateString = line.Substring(1, dateEndIndex - 1);
                
                int levelEndIndex = line.IndexOf(']', dateEndIndex + 1);
                if (levelEndIndex < 0) return false;
                
                string levelString = line.Substring(dateEndIndex + 2, levelEndIndex - dateEndIndex - 2);
                string message = line.Substring(levelEndIndex + 2);
                
                if (DateTime.TryParse(dateString, out DateTime timestamp) && 
                    Enum.TryParse<LogLevel>(levelString, out LogLevel level))
                {
                    entry = new LogEntry(message, level, timestamp);
                    return true;
                }
            }
            catch
            {
                // Invalid format, skip
            }
            
            return false;
        }
        
        public void Log(string message, LogLevel level)
        {
            var logEntry = new LogEntry(message, level, DateTime.Now);
            _logs.Add(logEntry);
            
            // Trim logs if too many
            if (_logs.Count > MaxLogsToKeep)
            {
                _logs.RemoveRange(0, _logs.Count - MaxLogsToKeep);
            }
            
            LogAdded?.Invoke(this, new LogEventArgs(message, level, DateTime.Now, logEntry));
        }
        
        public void ClearLogs()
        {
            _logs.Clear();
            
            // Delete the log file
            try
            {
                if (File.Exists(_logFilePath))
                {
                    File.Delete(_logFilePath);
                }
            }
            catch
            {
                // Failed to delete, ignore
            }
        }
        
        public void SaveLogs()
        {
            try
            {
                using (var writer = new StreamWriter(_logFilePath, false))
                {
                    foreach (var log in _logs)
                    {
                        writer.WriteLine(log.ToString());
                    }
                }
            }
            catch
            {
                // Failed to save logs, ignore
            }
        }
        
        public List<LogEntry> GetLogs()
        {
            return _logs.ToList();
        }
    }
} 