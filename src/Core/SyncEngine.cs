using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FlashSync.Core
{
    public class SyncEngine
    {
        private readonly string _sourceDir;
        private readonly string _targetDir;
        private readonly string _exfilesDir;
        private readonly string _exsizeDir;
        private readonly List<string> _excludeFolders = new List<string>();
        private readonly List<string> _selectedFiles; // For multi-selection support
        
        // Add logging event
        public event EventHandler<Services.LogEventArgs> LogEvent;
        public event EventHandler<SyncProgressEventArgs> SyncProgress;
        public event EventHandler<SyncCompletedEventArgs> SyncCompleted;
        
        // Constructor for syncing entire directories
        public SyncEngine(string sourceDir, string targetDir)
        {
            _sourceDir = sourceDir;
            _targetDir = targetDir;
            _exfilesDir = Path.Combine(_targetDir, "!EXFILES");
            _exsizeDir = Path.Combine(_targetDir, "!EXSIZE");
            _selectedFiles = null; // null means sync entire directory
            
            // Add the !EXFILES and !EXSIZE folders to exclude list
            _excludeFolders.Add(Path.GetFileName(_exfilesDir));
            _excludeFolders.Add(Path.GetFileName(_exsizeDir));
            
            Log("SyncEngine initialized for directory sync mode");
        }
        
        // Constructor for syncing selected files only
        public SyncEngine(string sourceDir, string targetDir, List<string> selectedFiles)
        {
            _sourceDir = sourceDir;
            _targetDir = targetDir;
            _exfilesDir = Path.Combine(_targetDir, "!EXFILES");
            _exsizeDir = Path.Combine(_targetDir, "!EXSIZE");
            _selectedFiles = selectedFiles;
            
            // Add the !EXFILES and !EXSIZE folders to exclude list
            _excludeFolders.Add(Path.GetFileName(_exfilesDir));
            _excludeFolders.Add(Path.GetFileName(_exsizeDir));
            
            Log($"SyncEngine initialized for selected files sync mode with {selectedFiles.Count} items");
        }
        
        public async Task SyncDirectoriesAsync()
        {
            try
            {
                Log("Starting synchronization...");
                
                // Get all files from source and target
                var sourceFiles = _selectedFiles == null ? GetFiles(_sourceDir) : GetSelectedFiles(_sourceDir);
                var targetFiles = GetFiles(_targetDir);
                
                // Filter out the !EXFILES and !EXSIZE folders from target files
                targetFiles = targetFiles.Where(f => 
                    !f.StartsWith(Path.Combine(_targetDir, "!EXFILES"), StringComparison.OrdinalIgnoreCase) &&
                    !f.StartsWith(Path.Combine(_targetDir, "!EXSIZE"), StringComparison.OrdinalIgnoreCase)).ToList();
                
                // Convert to relative paths for comparison
                var sourceRelativePaths = GetRelativePaths(sourceFiles, _sourceDir);
                var targetRelativePaths = GetRelativePaths(targetFiles, _targetDir);
                
                // Find files present in target but not in source (orphans)
                var orphanedFiles = targetRelativePaths.Except(sourceRelativePaths).ToList();
                
                // Find files to update (exist in both source and target)
                var commonFiles = sourceRelativePaths.Intersect(targetRelativePaths).ToList();
                
                // Find new files (exist only in source)
                var newFiles = sourceRelativePaths.Except(targetRelativePaths).ToList();
                
                // Find files with size differences
                var sizeMismatchFiles = await FindSizeMismatchFilesAsync(commonFiles);
                
                int totalFiles = orphanedFiles.Count + commonFiles.Count + newFiles.Count + sizeMismatchFiles.Count;
                int processedFiles = 0;
                
                // Only create !EXFILES directory if we have orphaned files
                if (orphanedFiles.Count > 0 && !Directory.Exists(_exfilesDir))
                {
                    Directory.CreateDirectory(_exfilesDir);
                    Log($"Created !EXFILES directory at {_exfilesDir}");
                }
                
                // Only create !EXSIZE directory if we have size mismatch files
                if (sizeMismatchFiles.Count > 0 && !Directory.Exists(_exsizeDir))
                {
                    Directory.CreateDirectory(_exsizeDir);
                    Log($"Created !EXSIZE directory at {_exsizeDir}");
                }
                
                // Process orphaned files (move to !EXFILES folder)
                await ProcessOrphanedFilesAsync(orphanedFiles, processedFiles, totalFiles);
                processedFiles += orphanedFiles.Count;
                
                // Process size mismatch files (move to !EXSIZE folder)
                await ProcessSizeMismatchFilesAsync(sizeMismatchFiles, processedFiles, totalFiles);
                processedFiles += sizeMismatchFiles.Count;
                
                // Process common files (update if needed)
                await ProcessCommonFilesAsync(commonFiles.Except(sizeMismatchFiles).ToList(), processedFiles, totalFiles);
                processedFiles += commonFiles.Count - sizeMismatchFiles.Count;
                
                // Process new files (copy from source to target)
                await ProcessNewFilesAsync(newFiles, processedFiles, totalFiles);
                
                Log($"Synchronization completed: {newFiles.Count} files added, {commonFiles.Count - sizeMismatchFiles.Count} files updated, {orphanedFiles.Count} files moved to !EXFILES, {sizeMismatchFiles.Count} files moved to !EXSIZE");
                
                // Notify completion
                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(true, null));
            }
            catch (Exception ex)
            {
                Log($"Error during synchronization: {ex.Message}", LogLevel.Error);
                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(false, ex.Message));
            }
        }
        
        private List<string> GetSelectedFiles(string baseDir)
        {
            List<string> files = new List<string>();
            
            foreach (var selectedPath in _selectedFiles)
            {
                var fullPath = Path.Combine(baseDir, selectedPath);
                
                if (File.Exists(fullPath))
                {
                    files.Add(fullPath);
                }
                else if (Directory.Exists(fullPath))
                {
                    files.AddRange(Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories));
                }
            }
            
            return files;
        }
        
        private async Task<List<string>> FindSizeMismatchFilesAsync(List<string> commonFiles)
        {
            var sizeMismatchFiles = new List<string>();
            
            await Task.Run(() =>
            {
                foreach (var relativePath in commonFiles)
                {
                    string sourceFile = Path.Combine(_sourceDir, relativePath);
                    string targetFile = Path.Combine(_targetDir, relativePath);
                    
                    if (File.Exists(sourceFile) && File.Exists(targetFile))
                    {
                        var sourceSize = new FileInfo(sourceFile).Length;
                        var targetSize = new FileInfo(targetFile).Length;
                        
                        if (sourceSize != targetSize)
                        {
                            sizeMismatchFiles.Add(relativePath);
                            Log($"Size mismatch found for {relativePath}: Source={sourceSize} bytes, Target={targetSize} bytes", LogLevel.Warning);
                        }
                    }
                }
            });
            
            return sizeMismatchFiles;
        }
        
        private async Task ProcessSizeMismatchFilesAsync(List<string> sizeMismatchFiles, int processedFiles, int totalFiles)
        {
            // If there are no size mismatch files, don't create any folders
            if (sizeMismatchFiles.Count == 0)
            {
                return;
            }

            await Task.Run(() =>
            {
                // Create a timestamp folder for this sync operation
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string timestampDir = Path.Combine(_exsizeDir, timestamp);
                
                // Ensure the timestamp directory exists
                if (!Directory.Exists(timestampDir))
                {
                    Directory.CreateDirectory(timestampDir);
                    Log($"Created timestamp directory for size mismatches: {timestampDir}");
                }
                
                Parallel.ForEach(sizeMismatchFiles, (relativePath) =>
                {
                    string targetFile = Path.Combine(_targetDir, relativePath);
                    string sourceFile = Path.Combine(_sourceDir, relativePath);
                    
                    // Get just the filename without path to preserve original filename
                    string fileName = Path.GetFileName(relativePath);
                    
                    // Create destination path in the timestamp folder
                    string exsizeFile = Path.Combine(timestampDir, fileName);
                    
                    // If a file with the same name already exists in the timestamp folder,
                    // append a number to make it unique
                    int counter = 1;
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);
                    
                    while (File.Exists(exsizeFile))
                    {
                        fileName = $"{fileNameWithoutExt}_{counter}{extension}";
                        exsizeFile = Path.Combine(timestampDir, fileName);
                        counter++;
                    }
                    
                    try
                    {
                        // Move file with size mismatch to !EXSIZE folder
                        if (File.Exists(targetFile))
                        {
                            File.Move(targetFile, exsizeFile);
                            
                            // Now copy the correct file from source
                            File.Copy(sourceFile, targetFile, false);
                            
                            Log($"Moved size-mismatched file to !EXSIZE: {relativePath}");
                            OnProgressUpdated(relativePath, ++processedFiles, totalFiles, SyncOperation.SizeMismatch);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error processing size mismatch file {targetFile}: {ex.Message}", LogLevel.Error);
                        Console.WriteLine($"Error processing size mismatch file {targetFile}: {ex.Message}");
                    }
                });
            });
        }
        
        private async Task ProcessOrphanedFilesAsync(List<string> orphanedFiles, int processedFiles, int totalFiles)
        {
            // If there are no orphaned files, don't create any folders
            if (orphanedFiles.Count == 0)
            {
                return;
            }

            await Task.Run(() =>
            {
                // Create a timestamp folder for this sync operation
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string timestampDir = Path.Combine(_exfilesDir, timestamp);
                
                // Ensure the timestamp directory exists
                if (!Directory.Exists(timestampDir))
                {
                    Directory.CreateDirectory(timestampDir);
                    Log($"Created timestamp directory for orphaned files: {timestampDir}");
                }
                
                Parallel.ForEach(orphanedFiles, (relativePath) =>
                {
                    string targetFile = Path.Combine(_targetDir, relativePath);
                    
                    // Get just the filename without path to preserve original filename
                    string fileName = Path.GetFileName(relativePath);
                    
                    // Create destination path in the timestamp folder
                    string extraFile = Path.Combine(timestampDir, fileName);
                    
                    // If a file with the same name already exists in the timestamp folder,
                    // append a number to make it unique
                    int counter = 1;
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);
                    
                    while (File.Exists(extraFile))
                    {
                        fileName = $"{fileNameWithoutExt}_{counter}{extension}";
                        extraFile = Path.Combine(timestampDir, fileName);
                        counter++;
                    }
                    
                    try
                    {
                        // Move file to !EXFILES folder with timestamp
                        if (File.Exists(targetFile))
                        {
                            File.Move(targetFile, extraFile);
                            Log($"Moved orphaned file to !EXFILES: {relativePath}");
                        }
                        
                        OnProgressUpdated(relativePath, ++processedFiles, totalFiles, SyncOperation.Moved);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error moving orphaned file {targetFile}: {ex.Message}", LogLevel.Error);
                        Console.WriteLine($"Error moving orphaned file {targetFile}: {ex.Message}");
                    }
                });
            });
        }
        
        private async Task ProcessCommonFilesAsync(List<string> commonFiles, int processedFiles, int totalFiles)
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(commonFiles, (relativePath) =>
                {
                    string sourceFile = Path.Combine(_sourceDir, relativePath);
                    string targetFile = Path.Combine(_targetDir, relativePath);
                    
                    try
                    {
                        if (ShouldUpdateFile(sourceFile, targetFile))
                        {
                            // Simply update the file without making a backup copy
                            File.Copy(sourceFile, targetFile, true);
                            Log($"Updated file: {relativePath}");
                            OnProgressUpdated(relativePath, ++processedFiles, totalFiles, SyncOperation.Updated);
                        }
                        else
                        {
                            OnProgressUpdated(relativePath, ++processedFiles, totalFiles, SyncOperation.Unchanged);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error updating file {targetFile}: {ex.Message}", LogLevel.Error);
                        Console.WriteLine($"Error updating file {targetFile}: {ex.Message}");
                    }
                });
            });
        }
        
        private async Task ProcessNewFilesAsync(List<string> newFiles, int processedFiles, int totalFiles)
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(newFiles, (relativePath) =>
                {
                    string sourceFile = Path.Combine(_sourceDir, relativePath);
                    string targetFile = Path.Combine(_targetDir, relativePath);
                    
                    try
                    {
                        // Create directory structure if needed
                        string targetFileDir = Path.GetDirectoryName(targetFile);
                        if (!Directory.Exists(targetFileDir))
                        {
                            Directory.CreateDirectory(targetFileDir);
                        }
                        
                        // Copy file from source to target
                        File.Copy(sourceFile, targetFile, false);
                        Log($"Added new file: {relativePath}");
                        
                        OnProgressUpdated(relativePath, ++processedFiles, totalFiles, SyncOperation.Added);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error copying new file {sourceFile}: {ex.Message}", LogLevel.Error);
                        Console.WriteLine($"Error copying new file {sourceFile}: {ex.Message}");
                    }
                });
            });
        }
        
        private bool ShouldUpdateFile(string sourceFile, string targetFile)
        {
            // First check: modification dates
            var sourceInfo = new FileInfo(sourceFile);
            var targetInfo = new FileInfo(targetFile);
            
            if (sourceInfo.LastWriteTimeUtc != targetInfo.LastWriteTimeUtc ||
                sourceInfo.Length != targetInfo.Length)
            {
                return true;
            }
            
            // For larger files, compare hashes
            if (sourceInfo.Length > 1024 * 1024) // 1MB threshold
            {
                return CompareFileHashes(sourceFile, targetFile);
            }
            
            return false;
        }
        
        private bool CompareFileHashes(string file1, string file2)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream1 = File.OpenRead(file1))
                using (var stream2 = File.OpenRead(file2))
                {
                    var hash1 = sha256.ComputeHash(stream1);
                    var hash2 = sha256.ComputeHash(stream2);
                    
                    return !hash1.SequenceEqual(hash2);
                }
            }
        }
        
        private List<string> GetFiles(string directory)
        {
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToList();
        }
        
        private List<string> GetRelativePaths(List<string> files, string baseDir)
        {
            return files.Select(f => Path.GetRelativePath(baseDir, f)).ToList();
        }
        
        private void OnProgressUpdated(string fileName, int processed, int total, SyncOperation operation)
        {
            SyncProgress?.Invoke(this, new SyncProgressEventArgs(fileName, processed, total, operation));
        }
        
        private void Log(string message, LogLevel level = LogLevel.Info)
        {
            LogEvent?.Invoke(this, new Services.LogEventArgs(message, level, DateTime.Now));
        }
    }
    
    public class SyncProgressEventArgs : EventArgs
    {
        public string FileName { get; }
        public int ProcessedFiles { get; }
        public int TotalFiles { get; }
        public SyncOperation Operation { get; }
        
        public SyncProgressEventArgs(string fileName, int processedFiles, int totalFiles, SyncOperation operation)
        {
            FileName = fileName;
            ProcessedFiles = processedFiles;
            TotalFiles = totalFiles;
            Operation = operation;
        }
    }
    
    public class SyncCompletedEventArgs : EventArgs
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        
        public SyncCompletedEventArgs(bool success, string errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage;
        }
    }
    
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
    
    public enum SyncOperation
    {
        Added,
        Updated,
        Moved,
        SizeMismatch,
        Unchanged
    }
} 