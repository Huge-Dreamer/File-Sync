using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Threading.Tasks;

namespace FlashSync.Services
{
    public class DriveDetectionService
    {
        private ManagementEventWatcher _insertWatcher;
        private ManagementEventWatcher _removeWatcher;
        private readonly List<string> _authorizedDrives = new List<string>();
        
        public event EventHandler<DriveDetectedEventArgs> DriveInserted;
        public event EventHandler<DriveRemovedEventArgs> DriveRemoved;
        
        public DriveDetectionService()
        {
            InitializeWatchers();
        }
        
        public void Start()
        {
            _insertWatcher.Start();
            _removeWatcher.Start();
        }
        
        public void Stop()
        {
            _insertWatcher.Stop();
            _removeWatcher.Stop();
        }
        
        public void Dispose()
        {
            _insertWatcher.Dispose();
            _removeWatcher.Dispose();
        }
        
        public void AddAuthorizedDrive(string driveId)
        {
            if (!_authorizedDrives.Contains(driveId))
            {
                _authorizedDrives.Add(driveId);
            }
        }
        
        public void RemoveAuthorizedDrive(string driveId)
        {
            if (_authorizedDrives.Contains(driveId))
            {
                _authorizedDrives.Remove(driveId);
            }
        }
        
        public bool IsDriveAuthorized(string driveId)
        {
            return _authorizedDrives.Contains(driveId);
        }
        
        private void InitializeWatchers()
        {
            try
            {
                // Create WMI query for drive insertion events
                var insertQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
                _insertWatcher = new ManagementEventWatcher(insertQuery);
                _insertWatcher.EventArrived += DriveInsertedEvent;
                
                // Create WMI query for drive removal events
                var removeQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3");
                _removeWatcher = new ManagementEventWatcher(removeQuery);
                _removeWatcher.EventArrived += DriveRemovedEvent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing drive watchers: {ex.Message}");
            }
        }
        
        private void DriveInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            var driveLetter = e.NewEvent.Properties["DriveName"].Value.ToString();
            
            // Get drive information
            var drive = new DriveInfo(driveLetter);
            
            if (drive.DriveType == DriveType.Removable || drive.DriveType == DriveType.Fixed)
            {
                // Generate a unique ID for the drive (based on volume serial number if available)
                string driveId = GetDriveId(driveLetter);
                
                DriveInserted?.Invoke(this, new DriveDetectedEventArgs(
                    driveLetter,
                    driveId,
                    drive.DriveType,
                    drive.IsReady ? drive.VolumeLabel : "Unknown",
                    drive.IsReady ? drive.TotalSize : 0,
                    drive.IsReady ? drive.AvailableFreeSpace : 0,
                    IsDriveAuthorized(driveId)
                ));
            }
        }
        
        private void DriveRemovedEvent(object sender, EventArrivedEventArgs e)
        {
            var driveLetter = e.NewEvent.Properties["DriveName"].Value.ToString();
            
            DriveRemoved?.Invoke(this, new DriveRemovedEventArgs(driveLetter));
        }
        
        private string GetDriveId(string driveLetter)
        {
            try
            {
                // Query WMI for volume information
                var query = $"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='{driveLetter}'";
                var searcher = new ManagementObjectSearcher(query);
                
                foreach (var queryObj in searcher.Get())
                {
                    return queryObj["VolumeSerialNumber"].ToString();
                }
                
                return Guid.NewGuid().ToString(); // Fallback to a random ID
            }
            catch
            {
                return Guid.NewGuid().ToString(); // Fallback to a random ID
            }
        }
    }
    
    public class DriveDetectedEventArgs : EventArgs
    {
        public string DriveLetter { get; }
        public string DriveId { get; }
        public DriveType DriveType { get; }
        public string VolumeLabel { get; }
        public long TotalSize { get; }
        public long AvailableFreeSpace { get; }
        public bool IsAuthorized { get; }
        
        public DriveDetectedEventArgs(string driveLetter, string driveId, DriveType driveType, 
                                     string volumeLabel, long totalSize, long availableFreeSpace, 
                                     bool isAuthorized)
        {
            DriveLetter = driveLetter;
            DriveId = driveId;
            DriveType = driveType;
            VolumeLabel = volumeLabel;
            TotalSize = totalSize;
            AvailableFreeSpace = availableFreeSpace;
            IsAuthorized = isAuthorized;
        }
    }
    
    public class DriveRemovedEventArgs : EventArgs
    {
        public string DriveLetter { get; }
        
        public DriveRemovedEventArgs(string driveLetter)
        {
            DriveLetter = driveLetter;
        }
    }
} 