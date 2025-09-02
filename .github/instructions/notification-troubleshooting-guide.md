# Notification System Troubleshooting Guide

## What Should Happen When You Run the App

### 1. **Debug Output (in Visual Studio Output Window)**
You should see these debug messages in order:

```
MauiProgram: CreateMauiApp started
MauiProgram: Starting service registration
NotificationManagerService constructor called
Initializing NotificationManagerService instance
Notification channel created for API 26+ (or "not needed for API < 26")
NotificationManagerCompat initialized successfully
MainActivity.OnCreate called
App.OnStart called
=== NOTIFICATION TEST STARTED ===
Context available: [ContextType]
NotificationManagerCompat created: [ManagerType]
Notifications enabled: True/False
Notification channel created
Direct notification sent successfully
=== NOTIFICATION TEST COMPLETED ===
```

### 2. **Notifications That Should Appear**
- **Test Notification (from MainActivity)**: "Direct Android API test notification" - appears ~2 seconds after app starts
- **Constructor Test**: "NotificationManagerService initialized successfully" - appears ~1 second after service creation
- **App Start Notification**: "App started - Notification sent" - from App.OnStart()
- **Periodic Notifications**: "Notification sent #1", "#2", etc. every 2 minutes

### 3. **Android Permissions**
The app should request notification permissions on Android 13+ (API 33+). Check:
- Settings → Apps → LogMyDay → Notifications → Allow notifications ✅

### 4. **Common Issues & Solutions**

#### **Issue: No Debug Output**
- **Cause**: App not running in debug mode or output window not visible
- **Solution**: Run from Visual Studio with Android emulator/device, check Output window

#### **Issue: "Platform.AppContext is null"**
- **Cause**: Service created too early in app lifecycle
- **Solution**: This is fixed by the delayed Task.Run calls

#### **Issue: "Notifications enabled: False"**
- **Cause**: User denied notification permissions
- **Solution**: 
  1. Go to Android Settings → Apps → LogMyDay → Notifications
  2. Enable "Allow notifications"
  3. Restart the app

#### **Issue: "Context or compatManager is null"**
- **Cause**: Service initialization failed
- **Solution**: Check Android API level and permissions

#### **Issue: Notification appears but no icon**
- **Cause**: Drawable resource not found
- **Solution**: Uses system fallback icon (should still work)

### 5. **Testing Steps**

1. **Deploy to Android device/emulator** (not desktop - won't work)
2. **Open Visual Studio Output window** → Select "Debug" from "Show output from"
3. **Launch the app**
4. **Check debug messages** - should see the sequence above
5. **Check Android notification panel** - pull down from top of screen
6. **Wait 2 minutes** - should see periodic notifications

### 6. **Verification Checklist**

- ✅ App builds without errors
- ✅ App runs on Android device/emulator (not desktop)
- ✅ Debug output shows service initialization
- ✅ "Notifications enabled: True" in debug output
- ✅ Test notification appears in Android notification panel
- ✅ Notification permissions granted in Android settings
- ✅ No error messages in debug output

### 7. **Manual Permission Grant**

If notifications don't appear:
1. Go to Android **Settings**
2. **Apps & notifications** (or just **Apps**)
3. Find **LogMyDay**
4. Tap **Notifications**
5. Enable **Allow notifications**
6. Restart the app

### 8. **Key Files Modified**
- `AndroidManifest.xml` - Added notification permissions
- `MainActivity.cs` - Added test notification call
- `NotificationManagerService.cs` - Fixed channel initialization
- `App.xaml.cs` - Added service resolution and notification startup
- `MauiProgram.cs` - Service dependency injection registration

The notification system is now comprehensive and should work on any Android device with proper permissions granted.

## If Still No Notifications

If you still don't see notifications after checking all the above:
1. **Check the debug output** - this will tell us exactly where it's failing
2. **Verify Android version** - needs Android 5.0+ (API 21+)
3. **Check device notification settings** - some devices have aggressive battery optimization
4. **Try on different device/emulator** - some emulators have quirks

The debug output is the key - it will show exactly what's happening at each step.
