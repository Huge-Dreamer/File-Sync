using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FlashSync.Core
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        public AppSettings Settings { get; private set; }
        
        public SettingsService()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashSync");
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            _settingsFilePath = Path.Combine(appDataPath, "settings.json");
            
            // Load or create settings
            LoadSettings();
        }
        
        private void LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return;
                }
                catch (Exception)
                {
                    // Failed to load settings, create new
                }
            }
            
            // Create default settings
            Settings = new AppSettings
            {
                AutoSyncEnabled = false,
                MaxConcurrentOperations = 4,
                UseLargeBuffers = true,
                AuthorizedDrives = new List<AuthorizedDrive>()
            };
        }
        
        public void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception)
            {
                // Failed to save settings
            }
        }
        
        public void ResetSettings()
        {
            // Create default settings
            Settings = new AppSettings
            {
                AutoSyncEnabled = false,
                MaxConcurrentOperations = 4,
                UseLargeBuffers = true,
                AuthorizedDrives = new List<AuthorizedDrive>()
            };
            
            // Delete the settings file
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    File.Delete(_settingsFilePath);
                }
            }
            catch
            {
                // Failed to delete settings file, ignore
            }
            
            // Save default settings
            SaveSettings();
        }
    }
    
    public class AppSettings
    {
        public bool AutoSyncEnabled { get; set; }
        public int MaxConcurrentOperations { get; set; }
        public bool UseLargeBuffers { get; set; }
        public List<AuthorizedDrive> AuthorizedDrives { get; set; }
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