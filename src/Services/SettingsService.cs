using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FlashSync.Services
{
    public class SettingsService
    {
        private const string SettingsFileName = "settings.json";
        private string _settingsFilePath;
        private ApplicationSettings _settings;
        
        public SettingsService()
        {
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FlashSync",
                SettingsFileName);
            
            // Ensure the settings directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath));
            
            LoadSettings();
        }
        
        public ApplicationSettings Settings => _settings;
        
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    _settings = JsonSerializer.Deserialize<ApplicationSettings>(json);
                }
                else
                {
                    // Create default settings
                    _settings = new ApplicationSettings
                    {
                        AutoSyncEnabled = true,
                        AuthorizedDrives = new List<AuthorizedDrive>(),
                        MaxConcurrentOperations = Environment.ProcessorCount,
                        UseLargeBuffers = true,
                        LastSyncPaths = new List<string>()
                    };
                    
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                
                // Create default settings if loading fails
                _settings = new ApplicationSettings
                {
                    AutoSyncEnabled = true,
                    AuthorizedDrives = new List<AuthorizedDrive>(),
                    MaxConcurrentOperations = Environment.ProcessorCount,
                    UseLargeBuffers = true,
                    LastSyncPaths = new List<string>()
                };
            }
        }
        
        public void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
        
        public void AddAuthorizedDrive(AuthorizedDrive drive)
        {
            // Check if the drive is already authorized by ID
            if (!_settings.AuthorizedDrives.Exists(d => d.DriveId == drive.DriveId))
            {
                _settings.AuthorizedDrives.Add(drive);
                SaveSettings();
            }
        }
        
        public void RemoveAuthorizedDrive(string driveId)
        {
            _settings.AuthorizedDrives.RemoveAll(d => d.DriveId == driveId);
            SaveSettings();
        }
        
        public bool IsDriveAuthorized(string driveId)
        {
            return _settings.AuthorizedDrives.Exists(d => d.DriveId == driveId);
        }
        
        public void AddRecentSyncPath(string path)
        {
            // Remove the path if it already exists
            _settings.LastSyncPaths.Remove(path);
            
            // Add to the beginning of the list
            _settings.LastSyncPaths.Insert(0, path);
            
            // Keep only the last 10 paths
            if (_settings.LastSyncPaths.Count > 10)
            {
                _settings.LastSyncPaths.RemoveAt(_settings.LastSyncPaths.Count - 1);
            }
            
            SaveSettings();
        }
        
        public void ResetSettings()
        {
            // Create default settings
            _settings = new ApplicationSettings
            {
                AutoSyncEnabled = true,
                AuthorizedDrives = new List<AuthorizedDrive>(),
                MaxConcurrentOperations = Environment.ProcessorCount,
                UseLargeBuffers = true,
                LastSyncPaths = new List<string>()
            };
            
            // Delete the settings file
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    File.Delete(_settingsFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting settings file: {ex.Message}");
            }
            
            // Save default settings
            SaveSettings();
        }
    }
    
    public class ApplicationSettings
    {
        public bool AutoSyncEnabled { get; set; }
        public List<AuthorizedDrive> AuthorizedDrives { get; set; }
        public int MaxConcurrentOperations { get; set; }
        public bool UseLargeBuffers { get; set; }
        public List<string> LastSyncPaths { get; set; }
    }
    
    public class AuthorizedDrive
    {
        public string DriveId { get; set; }
        public string DriveName { get; set; }
        public string VolumeLabel { get; set; }
        public string SyncFolderPath { get; set; }
        public DateTime LastSyncTime { get; set; }
    }
} 