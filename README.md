# File-Sync

A modern, efficient file synchronization utility for Windows.

## Features

- Synchronize files between local directories
- Automated sync with removable drives
- File selection mode for targeted synchronization
- Size comparison option
- Orphaned files handling
- Multi-threaded operations for improved performance
- Detailed logging
- Dark theme UI

## Requirements

- Windows 10 or higher
- .NET 7.0 (will be automatically installed if missing)

## Installation

1. Download the latest release from the [Releases](https://github.com/Huge-Dreamer/File-Sync/releases) page
2. Extract the ZIP file to a location of your choice
3. Run `File-Sync.exe`

## Usage

1. Select source and target directories
2. Click "Scan" to analyze differences
3. Select specific files (optional)
4. Click "Sync" to start synchronization

## Configuration

The application settings can be configured from the Settings tab:

- **Auto-sync**: Automatically sync when authorized drives are connected
- **Large buffers**: Use larger memory buffers for improved performance
- **Max concurrent operations**: Control the number of parallel sync operations

## Building from Source

```
git clone https://github.com/Huge-Dreamer/File-Sync.git
cd File-Sync
dotnet build --configuration Release
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Author

Created by [Huge-Dreamer](https://github.com/Huge-Dreamer) 