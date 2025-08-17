# ADB Debugging Guide for LogMyDay Mobile

## Overview
This guide documents how to use Android Debug Bridge (ADB) for debugging the LogMyDay mobile application crashes and issues.

## Prerequisites
- Android device connected via USB with USB Debugging enabled
- ADB installed and accessible from command line
- Device appears in `adb devices` output

## Essential ADB Commands

### 1. Check Connected Devices
```powershell
adb devices
```
Expected output: Shows connected device ID and status (device/unauthorized)

### 2. Clear Existing Logs (Critical First Step)
```powershell
adb logcat -c
```
**IMPORTANT**: Always clear logs before testing to avoid confusion with old logs

### 3. Monitor App-Specific Logs
```powershell
# General monitoring with context
adb logcat | Select-String -Pattern "LogMyDay|AndroidRuntime|FATAL|mono|dotnet" -Context 2

# Focus on crashes only
adb logcat -s AndroidRuntime:E

# Monitor specific app package
adb logcat | Select-String -Pattern "com.logmyday.mobile"
```

## Debugging Workflow

### Step 1: Prepare Monitoring
1. Clear existing logs: `adb logcat -c`
2. Start log monitoring (run in background terminal)
3. **Verify terminal is actually monitoring** - don't assume it's working

### Step 2: Test Application
1. **Uninstall app completely** if testing fresh install behavior
2. Install and launch the app
3. Reproduce the issue
4. **Wait for sufficient log output** before checking results

### Step 3: Analyze Results
1. Stop log monitoring processes: `Stop-Process -Name "adb" -Force`
2. Check terminal output using `get_terminal_output` tool
3. Look for specific error patterns:
   - `FATAL EXCEPTION`
   - `System.InvalidOperationException`
   - `CannotResolveService`
   - Dependency injection errors

## Common Issues and Solutions

### 1. Dependency Injection Errors
**Error Pattern**: `CannotResolveService, [ClassName], [AppName]`
**Solution**: Register missing services in `MauiProgram.cs`
```csharp
builder.Services.AddTransient<MissingService>();
```

### 2. Component Registration Issues  
**Error Pattern**: Problems with Blazor component resolution
**Solution**: Verify component references in MainPage.xaml and Routes.razor

### 3. Authentication/Navigation Issues
**Error Pattern**: App starts but never progresses past loading screen
**Investigation**: Check authentication flow, routing configuration, and initial navigation logic

## Critical Reminders

### ⚠️ Terminal State Awareness
- **NEVER assume terminal commands completed successfully**
- Always verify terminal is actively monitoring before testing
- Check that `adb devices` shows your device before starting
- Be aware that previous log outputs might still be visible in terminal

### ⚠️ Fresh Testing Approach
- Clear logs before each test session
- Uninstall app completely when testing fresh install scenarios
- Wait for adequate log output before concluding tests
- Stop all ADB processes between debugging sessions

### ⚠️ Log Interpretation
- Look for the **actual crash timestamp** vs old log entries
- Identify the **root cause** in the stack trace (usually first few lines)
- Don't get distracted by warning messages - focus on FATAL errors

## Useful Log Patterns to Watch For

```
# App starting
I ActivityManager: Start proc [PID]:com.logmyday.mobile

# Dependency injection issues  
E AndroidRuntime: CannotResolveService

# Component/routing issues
E AndroidRuntime: System.InvalidOperationException

# Authentication issues
# (Usually no fatal crash, just stuck behavior)

# Successful app launch
# (Normal activity logs without FATAL exceptions)
```

## PowerShell Specific Commands

```powershell
# Stop all ADB processes
Stop-Process -Name "adb" -Force -ErrorAction SilentlyContinue

# Background monitoring
adb logcat | Select-String -Pattern "LogMyDay|AndroidRuntime|FATAL" -Context 1

# Check if ADB is running
Get-Process | Where-Object {$_.Name -eq "adb"}
```

## File Location
This guide is stored in `.github/instructions/adb-debugging-guide.md` following the project documentation standards.
