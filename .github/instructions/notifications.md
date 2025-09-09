# Notification System

## Overview

LogMyDay features a comprehensive notification system designed to remind users about unfilled required activities. The system is built with a modular architecture that supports both in-app and system notifications.

## Current Implementation Status

✅ **Implemented**:
- System notifications when app is active/foreground
- Authentication-aware notification monitoring
- Smart notification content based on unfilled required activities
- Configurable timing intervals
- Platform-specific notification channels (Android)
- Comprehensive debugging and logging

🚧 **Future Enhancement**:
- Background notifications when app is closed/minimized
- Push notifications for real-time alerts
- Customizable notification schedules per user

## Architecture

### Core Components

#### SystemNotificationService
The main service responsible for monitoring and sending notifications about unfilled required activities.

**Key Features**:
- Automatically starts monitoring after user authentication
- Checks for unfilled required tags using the API endpoint `GetRequiredDailyTagsNotFilledForDate`
- Sends intelligent notifications with activity counts
- Stops monitoring when user logs out
- Configurable check intervals (currently 30 seconds for testing, 5 minutes for production)

**Location**: `LogMyDay.App.Mobile/Services/SystemNotificationService.cs`

#### NotificationService
Cross-platform wrapper service that handles platform-specific notification delivery.

**Key Features**:
- Abstracts platform-specific notification implementations
- Handles notification channels and permissions
- Provides consistent API across platforms
- Integrates with platform notification managers

**Location**: `LogMyDay.App.Mobile/Services/NotificationService.cs`

#### Platform-Specific Implementation
Android-specific notification manager that handles system notification delivery.

**Key Features**:
- Creates notification channels for Android 8.0+
- Manages notification permissions
- Handles notification priority and presentation
- Supports vibration and sound alerts

**Location**: `LogMyDay.App.Mobile/Platforms/Android/NotificationManagerService.cs`

### Integration Points

#### Authentication Integration
The notification system is tightly integrated with the authentication system:

```csharp
// Automatically starts/stops based on authentication state
_authService.AuthenticationChanged += OnAuthenticationChanged;
```

- **Login**: Monitoring starts automatically after successful authentication
- **Logout**: Monitoring stops immediately to respect user privacy
- **Session Management**: No background activity when user is not authenticated

#### API Integration
Uses the existing API infrastructure for checking unfilled activities:

```csharp
// Leverages the same API endpoint used by the Notifications page
var unfilledTags = await activityApi.GetRequiredDailyTagsNotFilledForDate(dateString);
```

## Configuration

### Timing Configuration
The system uses configurable intervals for checking unfilled activities:

- **Development/Testing**: 30 seconds for rapid feedback during development
- **Production**: 5 minutes for reasonable user experience without being intrusive

To change the interval, modify the timer configuration in `SystemNotificationService.cs`:

```csharp
// Current: 30 seconds for testing
_checkTimer = new System.Timers.Timer(30000);

// Production: 5 minutes
_checkTimer = new System.Timers.Timer(300000);
```

### Notification Content
Notifications provide intelligent messaging based on the number of unfilled activities:

- **Single Activity**: "You have 1 unfilled required activity for today"
- **Multiple Activities**: "You have X unfilled required activities for today"
- **No Activities**: Debug notification for testing (disabled in production)

## Implementation Details

### Service Registration
Services are registered in `MauiProgram.cs` with proper dependency injection:

```csharp
// Platform-specific notification service
builder.Services.AddSingleton<INotificationManagerService, NotificationManagerService>();

// Cross-platform wrapper
builder.Services.AddSingleton<NotificationService>();

// System notification monitoring
builder.Services.AddSingleton<ISystemNotificationService, SystemNotificationService>();
```

### Authentication Service Integration
Uses singleton pattern to ensure consistent authentication state:

```csharp
// Ensures the same AuthenticationService instance across the app
builder.Services.AddSingleton<AuthenticationService>(provider => AuthenticationService.Instance);
```

### Error Handling
Comprehensive error handling for network issues and authentication failures:

- **API Errors**: Graceful handling of network connectivity issues
- **Authentication Failures**: Automatic retry on next check cycle
- **Permission Issues**: Proper fallback when notification permissions are denied

## Debugging and Monitoring

### Debug Output
Extensive debug logging helps track notification system behavior:

```csharp
System.Diagnostics.Debug.WriteLine("SystemNotificationService: CheckForUnfilledTags started");
System.Diagnostics.Debug.WriteLine($"Found {unfilledTags.Count} unfilled required tags");
```

### Testing Tools
The `NotificationTester` class provides direct testing capabilities:

**Location**: `LogMyDay.App.Mobile/Tests/NotificationTester.cs`

**Usage**:
```csharp
NotificationTester.TestNotification(); // Call from any Android context
```

## Current Limitations

### Background Processing
The current implementation only works when the app is in the foreground or active state. This is by design for the initial implementation phase.

**Technical Constraints**:
- MAUI/Blazor WebView architecture limitations
- Android background processing restrictions
- Battery optimization considerations

### Platform Support
Currently implemented for Android platform only. iOS support planned for future releases.

## Future Enhancements

### Background Notifications
Planned improvements for background notification support:

1. **Android Foreground Service**: For consistent background monitoring
2. **Push Notifications**: Server-side scheduling and delivery
3. **Work Manager**: Android's recommended background task management
4. **iOS Background Tasks**: Native iOS background processing

### Advanced Features
Additional features planned for future releases:

- **Custom Notification Schedules**: User-configurable reminder times
- **Smart Notification Grouping**: Batch notifications to reduce interruptions
- **Rich Notifications**: Action buttons for quick activity logging
- **Notification History**: Track and review past notifications
- **Per-Tag Notifications**: Specific reminders for individual required tags

### Performance Optimizations
- **Intelligent Caching**: Reduce API calls through smart caching
- **Adaptive Timing**: Adjust check intervals based on user behavior
- **Battery Optimization**: Minimize power consumption during monitoring

## Security Considerations

The notification system respects user privacy and security:

- **Authentication Required**: No notifications without valid authentication
- **No Credential Storage**: Notifications don't expose sensitive user data
- **Privacy Respect**: Monitoring stops immediately upon logout
- **Secure API Calls**: All API communication uses existing security infrastructure

## Testing

### Manual Testing
1. **App Start**: Should see "🚀 App started - notification system active"
3. **Periodic Checks**: Notifications appear every 30 seconds (testing mode)
4. **Activity Content**: Notifications show correct count of unfilled activities

### Debug Output Verification
Monitor debug console for expected logging sequence:
```
SystemNotificationService: AuthenticationChanged event received - isAuthenticated: True
SystemNotificationService: User authenticated, starting monitoring
SystemNotificationService: Timer started - checking every 30 seconds (testing mode)
SystemNotificationService: CheckForUnfilledTags started
```

### Test Utilities
Use the `NotificationTester` class for direct platform testing:
- Verifies notification channel creation
- Tests notification delivery
- Validates platform integration
- Provides visual feedback through toast messages

## Maintenance

### Configuration Updates
- Review timer intervals before production deployment
- Update notification content for better user experience
- Monitor debug output to identify potential issues

### Performance Monitoring
- Track API call frequency and response times
- Monitor battery usage impact
- Collect user feedback on notification frequency and content

### Code Maintenance
- Regular review of error handling patterns
- Update platform-specific code for new Android/iOS versions
- Maintain compatibility with MAUI framework updates
