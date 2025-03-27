# FlashSync

FlashSync is a Windows application for safely synchronizing files between computers and external drives. It focuses on data safety by never deleting files during synchronization.

## Key Features

- **Non-destructive Synchronization**: Files are never deleted - orphaned files are moved to an "Extra" folder
- **Smart File Comparison**: Uses both modified dates and file hashes for accurate comparison
- **Context Menu Integration**: Right-click on folders to sync them with a drive
- **Auto-sync**: Automatically sync authorized drives when connected
- **Performance Optimization**: Multi-threaded operations with configurable concurrency

## System Requirements

- Windows 10/11
- .NET 7.0 Runtime or later
- Administrator privileges for context menu integration

## Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/FlashSync/releases) page
2. Run the installer
3. Follow the on-screen instructions

Alternatively, you can build from source:

```
git clone https://github.com/yourusername/FlashSync.git
cd FlashSync
dotnet build -c Release
```

## Usage

### Basic Synchronization

1. Launch FlashSync
2. Select a source directory
3. Select a target directory (e.g., an external drive)
4. Click "Scan" to analyze the directories
5. Review the list of files to be synchronized
6. Click "Sync Now" to begin synchronization

### Explorer Context Menu

Right-click on any folder and select "Sync with FlashSync" to open the application with that folder pre-selected as the source.

### Automatic Synchronization

1. Connect a drive
2. Add it to the authorized drives list
3. Enable automatic synchronization in settings
4. The drive will be automatically synchronized when connected in the future

## How It Works

FlashSync compares files between the source and target directories:

- New files (exist in source but not target) are copied to the target
- Updated files (exist in both but are different) are updated in the target
- Orphaned files (exist in target but not source) are moved to an "Extra" folder

File comparison is done using:
1. File size and modification date as a quick check
2. SHA-256 hash comparison for large files when needed

## Configuration

In the Settings tab, you can configure:

- Maximum concurrent operations
- Automatic synchronization
- Context menu integration
- Buffer sizes for improved performance

## Security and Safety

FlashSync prioritizes data safety:

- Files are never deleted during synchronization
- Administrator privileges are only required for context menu integration
- All operations are tracked with detailed progress information

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details. 