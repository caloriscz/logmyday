# Simple Debugging Hang Fix

## Problem
Visual Studio debugger hangs for minutes when stopping MAUI Android debugging sessions.

## Solution Applied
Added **ONE** setting to `LogMyDay.App.Mobile.csproj`:

```xml
<HotReloadEnabled>false</HotReloadEnabled>
```

## Why This Works
Hot Reload keeps background .NET processes running that prevent the debugger from cleanly detaching. Disabling it allows clean stop/start cycles.

## Trade-off
You must **rebuild** after code changes instead of using Hot Reload during debugging. This is acceptable since the debugger was unusable before.

## Testing
1. Start debugging (F5)
2. Stop debugging (Shift+F5)
3. Should complete within 5-10 seconds instead of minutes

## If Still Slow
If debugger still hangs after this change, you can manually kill stuck processes:

```powershell
# Kill ADB processes
Stop-Process -Name "adb" -Force -ErrorAction SilentlyContinue

# Kill Mono debugger
Stop-Process -Name "mono" -Force -ErrorAction SilentlyContinue

# Restart ADB
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" kill-server
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" start-server
```

## Pull-to-Refresh Status
**Separate issue - not addressed in this fix.** Pull-to-refresh behavior is untouched and works as before.
