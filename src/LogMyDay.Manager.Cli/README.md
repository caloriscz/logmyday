# LogMyDay Manager

Management CLI tool for LogMyDay - install, update, backup and restore operations.

## Overview

LogMyDay Manager is a .NET global tool that helps you:

- **Install** LogMyDay server from GitHub Releases
- **Update** existing installations to the latest version
- **Backup** user data to JSON files
- **Restore** data from backups
- **Status** check for services and configuration

## Installation

### Install as Global Tool

```powershell
dotnet tool install -g LogMyDay.Manager
```

### Update Existing Installation

```powershell
dotnet tool update -g LogMyDay.Manager
```

### Uninstall

```powershell
dotnet tool uninstall -g LogMyDay.Manager
```

## Usage

### Interactive Installation (Double-Click / No Arguments)

Simply run `logmyday` without arguments to start an interactive installation wizard:

```powershell
logmyday
```

This will guide you through:
- Prerequisite checking (.NET 9.0 SDK)
- Database provider selection (SQL Server or SQLite)
- Configuration generation
- Windows service registration
- Service startup

### Install LogMyDay

```powershell
# Interactive installation
logmyday install

# Specify installation options
logmyday install -p "C:\LogMyDay" -d SQLite -a "https://localhost:7064"
```

**Options:**
- `-p, --installPath` - Installation directory (default: `C:\Program Files\LogMyDay`)
- `-d, --dbProvider` - Database provider: `SqlServer` or `SQLite` (default: `SqlServer`)
- `-c, --connectionString` - Database connection string
- `-a, --apiAddress` - API base address (default: `https://localhost:7064`)

### Update LogMyDay

```powershell
logmyday update

# Specify installation path
logmyday update -p "C:\LogMyDay"
```

The update process:
1. Creates a timestamped backup of the current installation
2. Downloads the latest release from GitHub
3. Stops the Windows service
4. Replaces binaries (preserves configuration)
5. Restarts the service

### Backup Data

```powershell
# Backup to default file (timestamped)
logmyday backup -s https://localhost:7064

# Specify output file
logmyday backup -s https://localhost:7064 -o my-backup.json

# Provide credentials directly
logmyday backup -s https://localhost:7064 -u admin -p password123
```

**Options:**
- `-s, --serverUrl` - LogMyDay server URL (uses default if not specified)
- `-o, --outputPath` - Output file path (default: `logmyday-backup-{timestamp}.json`)
- `-u, --username` - Username
- `-p, --password` - Password

### Restore Data

```powershell
# Restore from backup
logmyday restore backup.json -s https://localhost:7064

# Restore and clear existing data first
logmyday restore backup.json -s https://localhost:7064 --clear-existing

# Provide credentials directly
logmyday restore backup.json -s https://localhost:7064 -u admin -p password123
```

**Options:**
- `backupFile` - Path to backup JSON file (required argument)
- `-s, --serverUrl` - LogMyDay server URL (uses default if not specified)
- `-c, --clearExisting` - Clear existing data before restore
- `-u, --username` - Username
- `-p, --password` - Password

### Check Status

```powershell
logmyday status

# Check specific service
logmyday status -s "LogMyDayApp"
```

Displays:
- Configuration file location
- Default server URL
- Last used server URL
- Local Windows service status
- Saved credentials

## Credential Management

LogMyDay Manager uses **Windows Credential Manager** to securely store server credentials. Credentials are stored per-server URL, allowing you to manage multiple LogMyDay instances.

When you run backup or restore commands for the first time, you'll be prompted for credentials and asked if you want to save them. Saved credentials are automatically used for future operations.

## Requirements

- **.NET 9.0 SDK** or later
- **Windows 10/11** (for Windows service management)
- **Administrator privileges** (for service installation)

## Architecture

### Project Structure

```
LogMyDay.Manager/
├── LogMyDay.Manager.Core/          # Core business logic
│   ├── Services/
│   │   ├── InstallationService     # Installation orchestration
│   │   ├── GitHubService           # GitHub API integration
│   │   ├── ConfigurationService    # Config file management
│   │   ├── WindowsCredentialService # Credential storage
│   │   ├── WindowsServiceManager   # Service management
│   │   └── PrerequisiteChecker     # System requirements
│   └── Models/
│       ├── InstallationConfig      # Installation configuration
│       ├── ServerCredential        # Credential model
│       └── PrerequisiteCheckResult # Check results
│
└── LogMyDay.Manager.Cli/           # CLI interface (global tool)
    ├── Commands/
    │   └── ManagerCommands         # Command implementations
    └── Program.cs                  # Application entry point
```

### Dependencies

- **Cocona** - CLI framework with command routing
- **CredentialManagement** - Windows Credential Manager integration
- **Refit** - Type-safe HTTP client for LogMyDay API
- **sc.exe** - Windows service management
- **GitHub API** - Release download and version checking

## Building from Source

### Build as NuGet Package

```powershell
.\build\build-manager.ps1
```

Output: `.\build\publish\manager\LogMyDay.Manager.*.nupkg`

### Install Local Package

```powershell
dotnet tool install -g LogMyDay.Manager --add-source .\build\publish\manager
```

### Development Build

```powershell
dotnet build src\LogMyDay.Manager.Cli\LogMyDay.Manager.Cli.csproj
```

## Security

- **Credentials** are stored in Windows Credential Manager (encrypted by Windows)
- **HTTPS** enforcement for all server communications
- **Per-server** credential storage (no credential sharing between servers)
- **Password input** is masked in interactive mode
- **No plaintext** credential storage in configuration files

## Version History

### v1.0.0 (Current)

Initial release with core features:
- ✅ Install from GitHub Releases
- ✅ Update to latest version
- ✅ Backup user data
- ✅ Restore from backup
- ✅ Status checking
- ✅ Windows Credential Manager integration
- ✅ .NET Global Tool distribution

## Future Enhancements

- Linux/macOS support (systemd services)
- Docker container management
- Multi-instance management dashboard
- Advanced reporting and statistics
- GUI wrapper for non-technical users

## Support

For issues, questions, or contributions, please visit:
- GitHub: https://github.com/caloriscz/logmyday
- Documentation: https://github.com/caloriscz/logmyday

## License

See [LICENSE](../LICENSE) file in the root repository.
