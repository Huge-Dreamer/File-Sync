using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using FlashSync.Core;
using FlashSync.Services;
using CoreSettingsService = FlashSync.Core.SettingsService;
using ServicesSettingsService = FlashSync.Services.SettingsService;
using CoreLoggingService = FlashSync.Core.LoggingService;
using ServicesLoggingService = FlashSync.Services.LoggingService;
using AuthorizedDrive = FlashSync.Services.AuthorizedDrive;
using LogLevel = FlashSync.Core.LogLevel;

namespace FlashSync.UI
{
    public partial class MainWindow : Window
    {
        private readonly ServicesSettingsService _settingsService;
        private readonly Services.DriveDetectionService _driveDetectionService;
        private readonly ServicesLoggingService _loggingService;
        private SyncEngine _syncEngine;
        
        private ObservableCollection<FileViewModel> _files = new ObservableCollection<FileViewModel>();
        private ObservableCollection<AuthorizedDrive> _authorizedDrives = new ObservableCollection<AuthorizedDrive>();
        
        private string _sourceDirectory;
        private string _targetDirectory;
        private bool _isSyncing = false;
        private bool _isSelectFilesMode = false;
        private List<string> _selectedFiles = new List<string>();
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize services
            _settingsService = new ServicesSettingsService();
            _driveDetectionService = new DriveDetectionService();
            _loggingService = new ServicesLoggingService();
            
            // Initialize a placeholder SyncEngine - will be replaced when actually syncing
            try
            {
                string tempDir = Path.GetTempPath();
                _syncEngine = new SyncEngine(tempDir, tempDir);
            }
            catch (Exception ex)
            {
                // If we can't create a placeholder SyncEngine, delay event subscriptions until needed
                System.Diagnostics.Debug.WriteLine($"Error initializing placeholder SyncEngine: {ex.Message}");
            }
            
            // Set data contexts
            FilesListView.ItemsSource = _files;
            DrivesListView.ItemsSource = _authorizedDrives;
            
            // Load settings
            LoadSettings();
            
            // Only subscribe to events if the SyncEngine was successfully initialized
            if (_syncEngine != null)
            {
                _syncEngine.SyncProgress += SyncEngine_SyncProgress;
                _syncEngine.SyncCompleted += SyncEngine_SyncCompleted;
                _syncEngine.LogEvent += SyncEngine_LogEvent;
            }
            
            // Subscribe to events
            _driveDetectionService.DriveInserted += DriveDetectionService_DriveInserted;
            // Use inline handler for DriveRemoved to avoid delegate type mismatch
            _driveDetectionService.DriveRemoved += (s, e) => UpdateDrivesList();
            
            // Start drive detection
            _driveDetectionService.Start();
            
            // Load authorized drives from settings
            LoadAuthorizedDrives();
            
            // Parse command line arguments
            ParseCommandLineArguments();
            
            // Set up logging
            SetupLogging();
            
            // Add a log entry for application start
            _loggingService.Log("Application started", LogLevel.Info);
        }
        
        private void SetupLogging()
        {
            _loggingService.LogAdded += (sender, e) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    // Add log to the textbox
                    LogsTextBox.AppendText(e.LogEntry.ToString() + Environment.NewLine);
                    LogsTextBox.ScrollToEnd();
                });
            };
            
            // Load existing logs if any
            LoadLogs();
        }
        
        private void LoadLogs()
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "File-Sync", "logs.txt");
            if (File.Exists(logPath))
            {
                try
                {
                    string logs = File.ReadAllText(logPath);
                    LogsTextBox.Text = logs;
                    LogsTextBox.ScrollToEnd();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to load logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void LoadSettings()
        {
            // Set max concurrent operations
            var maxConcurrentOps = _settingsService.Settings.MaxConcurrentOperations;
            for (int i = 0; i < MaxConcurrentComboBox.Items.Count; i++)
            {
                var item = MaxConcurrentComboBox.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == maxConcurrentOps.ToString())
                {
                    MaxConcurrentComboBox.SelectedIndex = i;
                    break;
                }
            }
            
            // Set auto sync
            AutoSyncCheckBox.IsChecked = _settingsService.Settings.AutoSyncEnabled;
            
            // Set large buffers
            LargeBuffersCheckBox.IsChecked = _settingsService.Settings.UseLargeBuffers;
        }
        
        private void LoadAuthorizedDrives()
        {
            _authorizedDrives.Clear();
            
            foreach (var drive in _settingsService.Settings.AuthorizedDrives)
            {
                _authorizedDrives.Add(drive);
            }
        }
        
        private void LoadRecentPaths()
        {
            // This method is no longer needed but keep it empty for compatibility
        }
        
        private void ParseCommandLineArguments()
        {
            string[] args = Environment.GetCommandLineArgs();
            
            if (args.Length > 1)
            {
                string path = args[1];
                
                if (Directory.Exists(path))
                {
                    // Set as source path
                    _sourceDirectory = path;
                    SourcePathComboBox.Text = path;
                    
                    // Check if any authorized drives are currently connected
                    foreach (var drive in _authorizedDrives)
                    {
                        if (Directory.Exists(drive.DriveName))
                        {
                            _targetDirectory = drive.DriveName;
                            TargetPathComboBox.Text = drive.DriveName;
                            break;
                        }
                    }
                }
            }
        }
        
        private void DriveDetectionService_DriveInserted(object sender, DriveDetectedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                // Update the status text
                StatusTextBlock.Text = $"Drive {e.DriveLetter} ({e.VolumeLabel}) inserted";
                _loggingService.Log($"Drive inserted: {e.DriveLetter} ({e.VolumeLabel})", LogLevel.Info);
                
                // Check if drive is authorized for auto-sync
                if (e.IsAuthorized && _settingsService.Settings.AutoSyncEnabled)
                {
                    // Find the authorized drive
                    var drive = _authorizedDrives.FirstOrDefault(d => d.DriveId == e.DriveId);
                    
                    if (drive != null)
                    {
                        // Set paths and start sync
                        _sourceDirectory = drive.SyncFolderPath;
                        _targetDirectory = e.DriveLetter;
                        
                        SourcePathComboBox.Text = _sourceDirectory;
                        TargetPathComboBox.Text = _targetDirectory;
                        
                        // Start scan and sync
                        ScanFiles();
                    }
                }
            });
        }
        
        private void BrowseSourceButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Select source directory",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _sourceDirectory = dialog.SelectedPath;
                SourcePathComboBox.Text = _sourceDirectory;
                _loggingService.Log($"Source directory set to {_sourceDirectory}", LogLevel.Info);
            }
        }
        
        private void BrowseTargetButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Select target directory",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _targetDirectory = dialog.SelectedPath;
                TargetPathComboBox.Text = _targetDirectory;
                _loggingService.Log($"Target directory set to {_targetDirectory}", LogLevel.Info);
            }
        }
        
        private void EnableSelectFilesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _isSelectFilesMode = true;
            FilesListView.SelectionMode = System.Windows.Controls.SelectionMode.Extended;
            SelectionControlsPanel.Visibility = Visibility.Visible;
            _loggingService.Log("File selection mode enabled", LogLevel.Info);
        }
        
        private void EnableSelectFilesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _isSelectFilesMode = false;
            FilesListView.SelectionMode = System.Windows.Controls.SelectionMode.Extended;
            SelectionControlsPanel.Visibility = Visibility.Collapsed;
            _loggingService.Log("File selection mode disabled", LogLevel.Info);
        }
        
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            FilesListView.SelectAll();
        }
        
        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            FilesListView.UnselectAll();
        }
        
        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            ScanFiles();
        }
        
        private async void ScanFiles()
        {
            if (string.IsNullOrEmpty(_sourceDirectory) || string.IsNullOrEmpty(_targetDirectory))
            {
                System.Windows.MessageBox.Show("Please select both source and target directories.", "Missing Directories", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!Directory.Exists(_sourceDirectory))
            {
                System.Windows.MessageBox.Show($"Source directory does not exist: {_sourceDirectory}", "Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            if (!Directory.Exists(_targetDirectory))
            {
                System.Windows.MessageBox.Show($"Target directory does not exist: {_targetDirectory}", "Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            _loggingService.Log($"Starting scan: Source={_sourceDirectory}, Target={_targetDirectory}", LogLevel.Info);
            
            // Clear files list
            _files.Clear();
            
            // Update UI
            StatusTextBlock.Text = "Scanning...";
            SyncProgressBar.Value = 0;
            SyncButton.IsEnabled = false;
            
            try
            {
                // Get all files from source and target
                var sourceFiles = Directory.GetFiles(_sourceDirectory, "*", SearchOption.AllDirectories);
                var targetFiles = Directory.GetFiles(_targetDirectory, "*", SearchOption.AllDirectories).Where(f => 
                    !f.StartsWith(Path.Combine(_targetDirectory, "!EXFILES"), StringComparison.OrdinalIgnoreCase) &&
                    !f.StartsWith(Path.Combine(_targetDirectory, "!EXSIZE"), StringComparison.OrdinalIgnoreCase)).ToArray();
                
                // Convert to relative paths for comparison
                var sourceRelativePaths = sourceFiles.Select(f => Path.GetRelativePath(_sourceDirectory, f)).ToList();
                var targetRelativePaths = targetFiles.Select(f => Path.GetRelativePath(_targetDirectory, f)).ToList();
                
                // Find files present in target but not in source (orphans)
                var orphanedFiles = targetRelativePaths.Except(sourceRelativePaths).ToList();
                
                // Find files to update (exist in both source and target)
                var commonFiles = sourceRelativePaths.Intersect(targetRelativePaths).ToList();
                
                // Find new files (exist only in source)
                var newFiles = sourceRelativePaths.Except(targetRelativePaths).ToList();
                
                // Find files with size differences
                var sizeMismatchFiles = new List<string>();
                foreach (var relativePath in commonFiles)
                {
                    string sourceFile = Path.Combine(_sourceDirectory, relativePath);
                    string targetFile = Path.Combine(_targetDirectory, relativePath);
                    
                    if (File.Exists(sourceFile) && File.Exists(targetFile))
                    {
                        var sourceSize = new FileInfo(sourceFile).Length;
                        var targetSize = new FileInfo(targetFile).Length;
                        
                        if (sourceSize != targetSize)
                        {
                            sizeMismatchFiles.Add(relativePath);
                        }
                    }
                }
                
                // Add files to the lists
                foreach (var file in newFiles)
                {
                    var sourceFile = Path.Combine(_sourceDirectory, file);
                    var fileInfo = new FileInfo(sourceFile);
                    
                    _files.Add(new FileViewModel
                    {
                        FileName = file,
                        Status = "New",
                        Size = FormatFileSize(fileInfo.Length),
                        ModifiedDate = fileInfo.LastWriteTime.ToString("g")
                    });
                }
                
                foreach (var file in orphanedFiles)
                {
                    var targetFile = Path.Combine(_targetDirectory, file);
                    var fileInfo = new FileInfo(targetFile);
                    
                    _files.Add(new FileViewModel
                    {
                        FileName = file,
                        Status = "Move to !EXFILES",
                        Size = FormatFileSize(fileInfo.Length),
                        ModifiedDate = fileInfo.LastWriteTime.ToString("g")
                    });
                }
                
                foreach (var file in sizeMismatchFiles)
                {
                    var sourceFile = Path.Combine(_sourceDirectory, file);
                    var targetFile = Path.Combine(_targetDirectory, file);
                    var sourceInfo = new FileInfo(sourceFile);
                    var targetInfo = new FileInfo(targetFile);
                    
                    _files.Add(new FileViewModel
                    {
                        FileName = file,
                        Status = "Size Mismatch",
                        Size = $"{FormatFileSize(targetInfo.Length)} → {FormatFileSize(sourceInfo.Length)}",
                        ModifiedDate = sourceInfo.LastWriteTime.ToString("g")
                    });
                }
                
                foreach (var file in commonFiles.Except(sizeMismatchFiles))
                {
                    var sourceFile = Path.Combine(_sourceDirectory, file);
                    var targetFile = Path.Combine(_targetDirectory, file);
                    var sourceInfo = new FileInfo(sourceFile);
                    var targetInfo = new FileInfo(targetFile);
                    
                    if (sourceInfo.LastWriteTime > targetInfo.LastWriteTime)
                    {
                        _files.Add(new FileViewModel
                        {
                            FileName = file,
                            Status = "Update",
                            Size = FormatFileSize(sourceInfo.Length),
                            ModifiedDate = sourceInfo.LastWriteTime.ToString("g")
                        });
                    }
                    else
                    {
                        _files.Add(new FileViewModel
                        {
                            FileName = file,
                            Status = "Unchanged",
                            Size = FormatFileSize(sourceInfo.Length),
                            ModifiedDate = sourceInfo.LastWriteTime.ToString("g")
                        });
                    }
                }
                
                // Update UI
                StatusTextBlock.Text = $"Scan completed: {newFiles.Count} new, {orphanedFiles.Count} orphaned, {sizeMismatchFiles.Count} size mismatched, {commonFiles.Count - sizeMismatchFiles.Count} common";
                SyncButton.IsEnabled = true;
                _loggingService.Log($"Scan completed: {newFiles.Count} new, {orphanedFiles.Count} orphaned, {sizeMismatchFiles.Count} size mismatched, {commonFiles.Count - sizeMismatchFiles.Count} common", LogLevel.Info);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error scanning files: {ex.Message}", "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Scan failed: " + ex.Message;
                _loggingService.Log($"Scan failed: {ex.Message}", LogLevel.Error);
            }
        }
        
        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isSyncing) return;
            
            if (string.IsNullOrEmpty(_sourceDirectory) || string.IsNullOrEmpty(_targetDirectory))
            {
                System.Windows.MessageBox.Show("Please select both source and target directories.", "Missing Directories", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // If in select files mode, get the selected files
            _selectedFiles.Clear();
            if (_isSelectFilesMode && FilesListView.SelectedItems.Count > 0)
            {
                foreach (FileViewModel item in FilesListView.SelectedItems)
                {
                    _selectedFiles.Add(item.FileName);
                }
                
                _loggingService.Log($"Starting sync with {_selectedFiles.Count} selected files", LogLevel.Info);
            }
            else
            {
                _loggingService.Log("Starting sync of all files", LogLevel.Info);
            }
            
            // Initialize the sync engine
            if (_isSelectFilesMode && _selectedFiles.Count > 0)
            {
                _syncEngine = new SyncEngine(_sourceDirectory, _targetDirectory, _selectedFiles);
            }
            else
            {
                _syncEngine = new SyncEngine(_sourceDirectory, _targetDirectory);
            }
            
            // Subscribe to events
            _syncEngine.SyncProgress += SyncEngine_SyncProgress;
            _syncEngine.SyncCompleted += SyncEngine_SyncCompleted;
            _syncEngine.LogEvent += SyncEngine_LogEvent;
            
            // Update UI
            _isSyncing = true;
            SyncButton.IsEnabled = false;
            ScanButton.IsEnabled = false;
            StatusTextBlock.Text = "Syncing...";
            SyncProgressBar.Value = 0;
            
            // Start the sync
            await Task.Run(() => _syncEngine.SyncDirectoriesAsync());
        }
        
        private void SyncEngine_LogEvent(object sender, Services.LogEventArgs e)
        {
            _loggingService.Log(e.Message, e.Level);
        }
        
        private void SyncEngine_SyncProgress(object sender, SyncProgressEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                // Update progress bar
                double progress = (double)e.ProcessedFiles / e.TotalFiles * 100;
                SyncProgressBar.Value = progress;
                
                // Update status text
                StatusTextBlock.Text = $"Syncing... {e.ProcessedFiles} of {e.TotalFiles} files processed. Current: {e.FileName} ({e.Operation})";
            });
        }
        
        private void SyncEngine_SyncCompleted(object sender, SyncCompletedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                // Reset sync state
                _isSyncing = false;
                SyncButton.IsEnabled = true;
                ScanButton.IsEnabled = true;
                
                if (e.Success)
                {
                    // Update status text
                    StatusTextBlock.Text = "Sync completed successfully";
                    _loggingService.Log("Sync completed successfully", LogLevel.Info);
                    
                    // Update last sync time for the drive
                    if (_authorizedDrives.Count > 0)
                    {
                        var drive = _authorizedDrives.FirstOrDefault(d => d.DriveName == _targetDirectory);
                        if (drive != null)
                        {
                            drive.LastSyncTime = DateTime.Now;
                            SaveSettings();
                        }
                    }
                }
                else
                {
                    // Show error message
                    System.Windows.MessageBox.Show($"Sync failed: {e.ErrorMessage}", "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    // Update status text
                    StatusTextBlock.Text = "Sync failed: " + e.ErrorMessage;
                    _loggingService.Log($"Sync failed: {e.ErrorMessage}", LogLevel.Error);
                }
                
                // Rescan files to update the UI
                ScanFiles();
            });
        }
        
        private void AddDriveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourceDirectory))
            {
                System.Windows.MessageBox.Show("Please select a source directory first.", "Missing Source", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Get drive info
            var driveInfo = DriveInfo.GetDrives().FirstOrDefault(d => _targetDirectory != null && _targetDirectory.StartsWith(d.Name, StringComparison.OrdinalIgnoreCase));
            
            if (driveInfo == null)
            {
                System.Windows.MessageBox.Show("Please select a target drive first.", "Missing Target", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Check if drive is already authorized
            var existingDrive = _authorizedDrives.FirstOrDefault(d => d.DriveId == driveInfo.VolumeLabel);
            
            if (existingDrive != null)
            {
                // Update existing drive
                existingDrive.SyncFolderPath = _sourceDirectory;
                existingDrive.LastSyncTime = DateTime.Now;
            }
            else
            {
                // Add new drive
                _authorizedDrives.Add(new AuthorizedDrive
                {
                    DriveName = driveInfo.Name.TrimEnd('\\'),
                    VolumeLabel = driveInfo.VolumeLabel,
                    DriveId = driveInfo.VolumeLabel,
                    SyncFolderPath = _sourceDirectory,
                    LastSyncTime = DateTime.Now
                });
            }
            
            // Save settings
            SaveSettings();
            
            _loggingService.Log($"Added drive to authorized list: {driveInfo.Name} ({driveInfo.VolumeLabel})", LogLevel.Info);
        }
        
        private void RemoveDriveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DrivesListView.SelectedItem is AuthorizedDrive selectedDrive)
            {
                // Remove drive
                _authorizedDrives.Remove(selectedDrive);
                
                // Save settings
                SaveSettings();
                
                _loggingService.Log($"Removed drive from authorized list: {selectedDrive.DriveName} ({selectedDrive.VolumeLabel})", LogLevel.Info);
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a drive to remove.", "No Drive Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void MaxConcurrentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveSettings();
        }
        
        private void CopyLogsButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText(LogsTextBox.Text);
            _loggingService.Log("Logs copied to clipboard", LogLevel.Info);
        }
        
        private void ClearLogsTabButton_Click(object sender, RoutedEventArgs e)
        {
            LogsTextBox.Clear();
            _loggingService.ClearLogs();
            _loggingService.Log("Logs cleared", LogLevel.Info);
        }
        
        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            LogsTextBox.Clear();
            _loggingService.ClearLogs();
            _loggingService.Log("Logs cleared", LogLevel.Info);
        }
        
        private void ResetAppButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to reset the application? This will clear all settings, logs, and cached data. The application will restart.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                _loggingService.Log("Application reset initiated", LogLevel.Warning);
                
                // Clear settings
                _settingsService.ResetSettings();
                
                // Clear logs
                _loggingService.ClearLogs();
                
                // Clear caches
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "File-Sync");
                if (Directory.Exists(appDataPath))
                {
                    try
                    {
                        Directory.Delete(appDataPath, true);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to delete application data: {ex.Message}", "Reset Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                
                // Restart the application
                Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                System.Windows.Application.Current.Shutdown();
            }
        }
        
        private void SaveSettings()
        {
            // Get max concurrent operations
            var selectedItem = MaxConcurrentComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                _settingsService.Settings.MaxConcurrentOperations = int.Parse(selectedItem.Content.ToString());
            }
            
            // Get auto sync
            _settingsService.Settings.AutoSyncEnabled = AutoSyncCheckBox.IsChecked ?? false;
            
            // Get large buffers
            _settingsService.Settings.UseLargeBuffers = LargeBuffersCheckBox.IsChecked ?? false;
            
            // Update authorized drives
            _settingsService.Settings.AuthorizedDrives = _authorizedDrives.ToList();
            
            // Save settings
            _settingsService.SaveSettings();
        }
        
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            
            // Save settings
            SaveSettings();
            
            // Stop drive detection
            _driveDetectionService.Stop();
            
            // Log application shutdown
            _loggingService.Log("Application shutting down", LogLevel.Info);
            _loggingService.SaveLogs();
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            
            return $"{size:0.##} {suffixes[suffixIndex]}";
        }
        
        private void UpdateDrivesList()
        {
            this.Dispatcher.Invoke(() =>
            {
                // Update the UI with current drives
                StatusTextBlock.Text = "Drive list updated";
                _loggingService.Log("Drive list updated", LogLevel.Info);
            });
        }
    }
    
    public class FileViewModel
    {
        public string FileName { get; set; }
        public string Status { get; set; }
        public string Size { get; set; }
        public string ModifiedDate { get; set; }
    }
} 