# LogMyDay Installer

Console installer and management tool for LogMyDay.

## Features

- **install** - Install LogMyDay server with guided setup
  - Prerequisite checking (.NET 9.0 SDK)
  - Database provider selection (SQL Server or SQLite)
  - Configuration generation
  - Windows service registration
  - Start Menu shortcut creation

- **configure** - Modify LogMyDay configuration
  - Update database connection
  - Change API base address
  - Configure email settings
  - Automatic service restart

- **backup** - Export user data to JSON file
  - Secure backup (user-scoped data only)
  - Per-server credential storage
  - Windows Credential Manager integration

- **restore** - Import user data from backup
  - Optional clear existing data
  - Full validation and error reporting

- **update** - Upgrade to latest version
  - GitHub release integration
  - Automatic backup before update
  - Configuration preservation

- **status** - Check service status
  - Installation verification
  - Service running state

## Usage

### Install LogMyDay

```powershell
# Interactive installation
logmyday install

# Specify installation options
logmyday install -p "C:\LogMyDay" -d SQLite -a "https://localhost:7064"
```

### Backup Data

```powershell
# Backup to default file
logmyday backup -s https://localhost:7064

# Specify output file
logmyday backup -s https://localhost:7064 -o backup.json
```

### Restore Data

```powershell
# Restore from backup
logmyday restore backup.json -s https://localhost:7064

# Restore and clear existing data
logmyday restore backup.json -s https://localhost:7064 --clear-existing
```

### Update LogMyDay

```powershell
logmyday update
```

### Configure Settings

```powershell
logmyday configure
```

### Check Status

```powershell
logmyday status
```

## Credential Management

The installer uses Windows Credential Manager to securely store server credentials. Credentials are stored per-server URL, allowing you to manage multiple LogMyDay instances.

When you run backup or restore commands for the first time, you'll be prompted for credentials and asked if you want to save them. Saved credentials are automatically used for future operations.

## Requirements

- Windows 10/11
- .NET 9.0 SDK (checked during installation)
- SQL Server (for SQL Server provider) or writable directory (for SQLite)
- Administrator privileges (for service installation)

## Building

To build the installer as a self-contained executable:

```powershell
.\build\build-installer.ps1
```

Output: `.\build\publish\installer\logmyday.exe`

## Architecture

- **Cocona** - CLI framework with command routing
- **CredentialManagement** - Windows Credential Manager integration
- **Refit** - Type-safe HTTP client for LogMyDay API
- **sc.exe** - Windows service management
- **GitHub API** - Release download and version checking

## Project Structure

```
LogMyDay.Installer/
├── Commands/
│   └── InstallerCommands.cs      # CLI command implementations
├── Services/
│   ├── WindowsCredentialService.cs    # Credential storage
│   ├── GitHubService.cs               # GitHub API integration
│   ├── ConfigurationService.cs        # Config file management
│   ├── WindowsServiceManagerService.cs # Service management
│   ├── PrerequisiteChecker.cs         # System requirements
│   └── InstallationService.cs         # Installation orchestration
├── Models/
│   ├── ServerCredential.cs        # Credential model
│   ├── InstallationConfig.cs      # Installation configuration
│   └── PrerequisiteCheckResult.cs # Check results
└── Program.cs                     # Application entry point
```

## Future Enhancements

- Linux/macOS support (systemd services)
- Batch upload command for bulk data import
- Advanced reporting and statistics
- GUI wrapper for non-technical users
- Multi-instance management dashboard
