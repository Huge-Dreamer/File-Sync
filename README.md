# File-Sync

Simple and efficient file synchronization utility for Windows.

## Download

📥 [Download the latest release (v1.0.0)](https://github.com/Huge-Dreamer/File-Sync/releases/tag/v1.0.0)

## Features

- Synchronize files between local directories
- Automated sync with removable drives
- File selection mode for targeted synchronization
- Size comparison option
- Orphaned files handling with version preservation in "!EXFILES" folder
- Multi-threaded operations for improved performance
- Large buffer option for faster transfers
- Detailed logging
- Modern dark theme UI

## Requirements

- Windows 10 or higher
- .NET 7.0 (included in the release package)

## Installation

1. Download the latest release from the [Releases](https://github.com/Huge-Dreamer/File-Sync/releases) page
2. Run `File-Sync.exe` or Extract the ZIP file to a location of your choice
3. Administrator privileges required for some features

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

This project is licensed under the Apache 2.0 License - see the [LICENSE](LICENSE) file for details.

## Author

Created by [Huge-Dreamer](https://github.com/Huge-Dreamer) 
