# LogMyDay Build System

This directory contains the Cake build system for automated deployment using Web Deploy. It contains cake build system for automated deployment using Web Deploy instead of FTP.This directory contains the Cake build system for automated deployment using Web Deploy instead of FTP.

- **build.cake**: Main build script with Web Deploy functionality and test integration
- **build.ps1**: PowerShell bootstrapper for Windows
- **build.sh**: Bash bootstrapper for Linux/macOS## Files## Files
- **tools/packages.config**: NuGet packages for Cake build system

## Available Build Targets

- **build.cake**: Main build script with Web Deploy functionality

### 🛡️ Recommended Deployment Targets

- **build.ps1**: PowerShell bootstrapper for Windows- **build.ps1**: PowerShell bootstrapper for Windows

#### `Default` - Full Safe Deployment

```powershell- **build.sh**: Bash bootstrapper for Linux/macOS- **build.sh**: Bash bootstrapper for Linux/macOS

.\build\build.ps1

```- **tools/packages.config**: NuGet packages for Cake build system- **tools/packages.config**: NuGet packages for Cake build system

- **Pipeline**: Clean → Restore → Build → **Test** → Publish → Package → Deploy

- ✅ **Runs all unit tests before deployment**
- ✅ **Deployment blocked if ANY test fails**
- ✅ **Use for all production deployments**## Available Build Targets## Available Build Targets


#### `FastDeploy` - Quick Safe Deployment (RECOMMENDED)

```powershell

.\build\build.ps1 -Target FastDeploy

```

- ✅ **Runs all unit tests before deployment**

- ✅ Skips packaging step for faster deployment- `CI`: Continuous integration build (Clean → Restore → Build → Test → Package)- `CI`: Continuous integration build (Clean → Restore → Build → Test → Package)

- ✅ **RECOMMENDED for pre-production deployments**

- **~30% faster than Default while maintaining test protection**- `FastDeploy`: Quick deployment without tests (Clean → Restore → Build → Publish → Package → Deploy)- `FastDeploy`: Quick deployment without tests (Clean → Restore → Build → Publish → Package → Deploy)

#### `CI` - Continuous Integration Build

```powershell

.\build\build.ps1 -Target CI### Individual Tasks### Individual Tasks

```

- **Pipeline**: Clean → Restore → Build → **Test** → Package- `Clean`: Clean build output and temporary directories- `Clean`: Clean build output and temporary directories

- ✅ Runs all tests and creates package
- ❌ Does not deploy- `Restore`: Restore NuGet packages- `Restore`: Restore NuGet packages
- ✅ Use for CI/CD pipelines

- `Build`: Build the solution- `Build`: Build the solution

### ⚠️ Emergency-Only Targets

- `Test`: Run unit tests- `Test`: Run unit tests

#### `DeployUnsafe` - Emergency Deployment WITHOUT Tests

```powershell- `Publish`: Publish the application for deployment- `Publish`: Publish the application for deployment

.\build\build.ps1 -Target DeployUnsafe

```- `Package`: Create deployment package- `Package`: Create deployment package

- **Pipeline**: Clean → Restore → Build → Publish → Deploy

- ❌ **BYPASSES ALL TESTS**- `Deploy`: Deploy using Web Deploy- `Deploy`: Deploy using Web Deploy
- ⚠️ **Use ONLY for emergency hotfixes**
- ⚠️ **Manual verification required after deployment**
- 🚨 **Big warning messages displayed during execution**

## Required Environment Variables (GitHub Secrets)## Required Environment Variables (GitHub Secrets)

### Individual Tasks

- `Clean`: Clean build output and temporary directories
- `Restore`: Restore NuGet packagesThe build script expects the following environment variables to be set with GitHub secrets:The build script expects the following environment variables to be set with GitHub secrets:
- `Build`: Build the solution
- `Test`: Run all 57 unit tests (security, data integrity, performance)
- `Publish`: Publish the application for deployment
- `Package`: Create deployment package (zip)- `LMD_SERVER`: Web Deploy server hostname/IP (e.g., site28117.siteasp.net)- `LMD_SERVER`: Web Deploy server hostname/IP (e.g., site28117.siteasp.net)
- `Deploy`: Deploy using Web Deploy
- `ValidateDeploymentConfig`: Verify environment variables are set- `LMD_PORT`: Web Deploy port (default: 8172)- `LMD_PORT`: Web Deploy port (default: 8172)

## Test Protection- `LMD_SITE`: Target site name (e.g., site28117)- `LMD_SITE`: Target site name (e.g., site28117)

**NEW**: All deployment targets now run the full test suite (57 tests) before deployment:- `LMD_LOGIN`: Web Deploy username (e.g., site28117)- `LMD_LOGIN`: Web Deploy username (e.g., site28117)

### Test Categories- `LMD_PASSWORD`: Web Deploy password- `LMD_PASSWORD`: Web Deploy password

- **Authentication Security** (13 tests): Password hashing, duplicate prevention, authorization
- **Rate Limiting** (13 tests): Brute-force protection, progressive lockout
- **Data Integrity** (4 tests): User isolation, backup/restore validation
- **Performance** (8 tests): Query optimization, LEFT JOIN validation## Usage## Usage
- **Service Logic** (19 tests): Activity, Tag, Unit service logic

### Test Failure Behavior

```### Local Development### Local Development

🧪 RUNNING TESTS - Deployment will be blocked if tests fail

========================================```powershell```powershell

... test output ...

========================================# Full deployment# Full deployment

❌ TESTS FAILED - DEPLOYMENT ABORTED

========================================.\build\build.ps1.build`build`ps1

Error: dotnet test failed - fix the failing tests before deploying

```



**Deployment will NEVER proceed if tests fail.**# Specific target# Specific target

## Required Environment Variables.\build\build.ps1 -Target Build.buildbuild.ps1 -Target Build

The build script expects the following environment variables:

### Web Deploy Configuration# Fast deployment without tests# Fast deployment without tests

- `LMD_SERVER`: Web Deploy server hostname/IP (e.g., site28117.siteasp.net)
- `LMD_PORT`: Web Deploy port (default: 8172).\build\build.ps1 -Target FastDeploy.buildbuild.ps1 -Target FastDeploy
- `LMD_SITE`: Target site name (e.g., site28117)
- `LMD_LOGIN`: Web Deploy username
- `LMD_PASSWORD`: Web Deploy password

# CI build only (no deployment)# CI build only (no deployment)

### Local Development Setup

.\build\build.ps1 -Target CI.buildbuild.ps1 -Target CI

Create a `local.env` file in the `build/` directory (gitignored):

``````

```bash

LMD_SERVER=your-server.com

LMD_PORT=8172

LMD_SITE=your-site-name### GitHub Actions### GitHub Actions

LMD_LOGIN=your-username

LMD_PASSWORD=your-passwordThe build script is designed to work with GitHub Actions where environment variables are populated from GitHub Secrets.The build script is designed to work with GitHub Actions where environment variables are populated from GitHub Secrets.

```

The build script will automatically load these variables.

## Web Deploy Benefits## Web Deploy Benefits

## Usage Examples

### Local Development

- **Speed**: Much faster than FTP transfers- **Speed**: Much faster than FTP transfers

```powershell

# Recommended: Fast deployment with test protection- **Reliability**: Built-in retry logic and error handling- **Reliability**: Built-in retry logic and error handling

.\build\build.ps1 -Target FastDeploy

- **Atomicity**: Changes are applied atomically- **Atomicity**: Changes are applied atomically

# Full deployment with packaging

.\build\build.ps1- **Incremental**: Only deploys changed files- **Incremental**: Only deploys changed files


# Run tests only (no deployment)- **Integration**: Native integration with IIS and Azure- **Integration**: Native integration with IIS and Azure

.\build\build.ps1 -Target Test

# Build and package only (for CI)

.\build\build.ps1 -Target CI## Troubleshooting## Troubleshooting

# Emergency deployment (TESTS BYPASSED - USE WITH CAUTION)

.\build\build.ps1 -Target DeployUnsafe

```If deployment fails, check:If deployment fails, check:


### GitHub Actions1. Web Deploy is installed on target server1. Web Deploy is installed on target server

The build script is designed to work with GitHub Actions where environment variables are populated from GitHub Secrets.2. Management Service is running on target server2. Management Service is running on target server

Example workflow:3. Firewall allows connections on specified port3. Firewall allows connections on specified port

```yaml

- name: Deploy to Pre-Production4. Deployment credentials have sufficient permissions4. Deployment credentials have sufficient permissions

  run: ./build/build.ps1 -Target FastDeploy

  env:5. Target site exists and is properly configured5. Target site exists and is properly configured

    LMD_SERVER: ${{ secrets.LMD_SERVER }}

    LMD_SITE: ${{ secrets.LMD_SITE }}

    LMD_LOGIN: ${{ secrets.LMD_LOGIN }}

    LMD_PASSWORD: ${{ secrets.LMD_PASSWORD }}The build script provides detailed error messages and troubleshooting tips when deployment fails.The build script provides detailed error messages and troubleshooting tips when deployment fails.

```

## Web Deploy Benefits

- **Speed**: Much faster than FTP transfers
- **Reliability**: Built-in retry logic and error handling
- **Atomicity**: Changes are applied atomically
- **Incremental**: Only deploys changed files
- **Integration**: Native integration with IIS and Azure
- **App Offline**: Automatically takes app offline during deployment

## Troubleshooting

### Deployment Fails

If deployment fails, check:
1. Web Deploy is installed on target server
2. Management Service is running on target server
3. Firewall allows connections on specified port (usually 8172)
4. Deployment credentials have sufficient permissions
5. Target site exists and is properly configured
6. msdeploy.exe is installed locally and in PATH

The build script provides detailed error messages and troubleshooting tips when deployment fails.

### Tests Fail

If tests fail and block deployment:
1. Run tests locally: `dotnet test src/LogMyDay.Api.Tests/LogMyDay.Api.Tests.csproj`
2. Fix the failing tests
3. Commit and retry deployment
4. **Never use `DeployUnsafe` to bypass tests unless it's a true emergency**

### Build Performance

- **Default**: ~60-90 seconds (full pipeline with packaging)
- **FastDeploy**: ~40-60 seconds (skips packaging)
- **Test**: ~5-10 seconds (just the test suite)
- **DeployUnsafe**: ~30-40 seconds (no tests - NOT RECOMMENDED)

## Migration Notes

**BREAKING CHANGE**: The `FastDeploy` target now includes test execution (previously it bypassed tests).

- **Old behavior**: `FastDeploy` skipped tests entirely
- **New behavior**: `FastDeploy` runs all tests before deployment
- **Migration**: If you were using `FastDeploy` to skip tests, use `DeployUnsafe` (but understand the risks!)

**Rationale**: Test protection is critical. The old `FastDeploy` behavior allowed broken code to reach production. The new behavior maintains safety while still being faster than the full `Default` pipeline.
