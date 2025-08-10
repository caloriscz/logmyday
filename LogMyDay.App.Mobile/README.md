# LogMyDay Mobile App

This is the mobile companion app for LogMyDay, built with .NET MAUI. It provides a mobile-friendly interface for the web application and includes Quick Activities functionality.

## Features

### 1. Bottom Navigation
- **Home Tab**: Contains a WebView that displays the LogMyDay web application
- **Quick Activities Tab**: Allows creating and using quick activity buttons for rapid activity logging

### 2. Quick Activities
- **Create Quick Activity Buttons**: Choose a tag, set a value, and create a reusable button
- **One-Tap Activity Creation**: Tap a button to instantly create an activity with pre-configured values
- **15-Second Cooldown**: After using a button, it's disabled for 15 seconds to prevent accidental double-clicks
- **Button Management**: Add and remove quick activity buttons as needed
- **Persistent Storage**: Button configurations are saved locally using Preferences API

### 3. API Integration
- Uses Refit for type-safe HTTP client communication with the LogMyDay API
- Supports the same authentication system as the web application
- Real-time activity creation and tag fetching

## Technical Details

### Architecture
- **Clean Architecture**: Follows separation of concerns with Services, ViewModels, and Views
- **Dependency Injection**: All services are properly registered and injected
- **MVVM Pattern**: Uses proper data binding and command patterns for UI interaction

### Key Components
- `HomePage`: Contains the WebView for the main application
- `QuickActivitiesPage`: Manages quick activity buttons
- `ApiService`: Handles API communication using Refit
- `QuickActivityService`: Manages button persistence and cooldown logic
- `BasicAuthHandler`: Handles authentication for API calls

### Data Models
- `QuickActivityButton`: Represents a configurable quick activity button
- Uses existing DTOs from LogMyDay.Shared for API communication

## Setup Instructions

### Prerequisites
- .NET 9 SDK
- Android SDK (for Android development)
- Visual Studio 2022 with MAUI workload installed

### Configuration
1. **API Endpoint**: Configure the base URL in `MauiProgram.cs`
   - Development: `http://localhost:5000` (default)
   - Production: `https://logmyday.tadata.cz` (default)

2. **Authentication**: Update credentials in `MauiProgram.cs`
   ```csharp
   new BasicAuthHandler("your_username", "your_password")
   ```

### Building and Running
1. Open the solution in Visual Studio 2022
2. Select the Android target
3. Build and deploy to an Android device or emulator

### Usage
1. **Home Tab**: Browse and use the web application normally
2. **Quick Activities Tab**:
   - Tap "+ Add" to create a new quick activity button
   - Select a tag from available tags
   - Enter a name for the button
   - Optionally set a default value based on tag type
   - Tap the created button to instantly log an activity
   - Use the "✕" button to delete unwanted quick activities

### Notes
- The app requires network connectivity to communicate with the LogMyDay API
- Quick activity buttons are stored locally on the device
- Button cooldowns reset when the app is restarted
- Tag list is fetched from the API, so create tags in the web application first

## Future Enhancements
- Configurable authentication (login screen)
- Offline support for quick activities
- Enhanced UI with custom icons
- Push notifications for activity reminders
- Export/import quick activity button configurations
