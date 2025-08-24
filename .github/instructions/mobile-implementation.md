# Mobile Client Architecture (Aug 2025 Refactor)

This document captures the mobile (LogMyDay.App.Mobile) implementation after the August 2025 refactor that resolved the `net_http_operation_started` exceptions and phantom logout host (`https://0.0.0.1`).
## Overview
The .NET MAUI mobile app embeds a BlazorWebView and connects to a user‑selected LogMyDay server instance. Users can change server + credentials at runtime without restarting the app.

## Core Principles
- Do not mutate an in-use `HttpClient` (no changing `BaseAddress` / `DefaultRequestHeaders` after first request).
- Build new `HttpClient` instances via `IHttpClientFactory` when context changes.
- Keep password in memory only; never persist it. Persist server URL + username for UX only.

- Refit clients are created on demand, not registered with a static base URL at DI startup.

## Key Components
| Component | Responsibility |
|-----------|----------------|
| `ApiContext` | Holds current `Server` (Uri), `Username`, `Password`; change notification event. |
| `DynamicAuthHandler` | Injects Basic Auth header using current context just-in-time. |
| `ApiClientProvider` | Lazily builds Refit interfaces with a fresh `HttpClient` per context version. |
| `Preferences` | Stores `ServerUrl`, `Username` (NOT password). |
| Login Page | Validates URL, configures context, performs probe call (`GetTags`) to confirm connectivity. |
| Logout (MobileTopbar) | Clears authentication state + context + optional username preference; navigates to `/login`. |

## Lifecycle
1. App starts → loads saved server URL & username (if any) into login form.
2. User submits credentials:
  - Validate absolute HTTPS URL (prepend `https://` if scheme omitted).
  - Call `ApiContext.Configure(server, username, password)`.
  - First API probe triggers `ApiClientProvider` to build a new Refit client (`dynamic-api` named client + `DynamicAuthHandler`).
3. Subsequent requests reuse cached Refit interface until context changes.
4. On logout → `ApiContext.Clear()` invalidates provider; any future API usage before re-login throws a controlled "not configured" error if attempted.

## Why the Previous Approach Failed
| Issue | Old Pattern | New Pattern |
|-------|-------------|-------------|
| Dynamic server switching | Mutated singleton `HttpClient.BaseAddress` | Rebuild client per context change |
| Credentials header | Set via mutated `DefaultRequestHeaders` | Injected per request by handler |
| Logout artifact `https://0.0.0.1` | Placeholder / invalid base during mutation window | No mutation; either valid server or unconfigured state |
| net_http_operation_started | Property mutation after first request | Never mutate after first use |

## Adding a New API Interface (Mobile)
1. Define interface in `LogMyDay.Shared` (e.g., `IBackupApi`).
2. Extend `ApiClientProvider`:
```csharp
private IBackupApi? _backup;
public IBackupApi Backup => _backup ??= Build<IBackupApi>();
```
3. Inject `IApiClientProvider` where needed and use `provider.Backup`.
4. No DI registration for the Refit interface itself; provider manages lifecycle.

## Validation Checklist
- URL validated & normalized (ensure HTTPS).
- Context configured before first API call.
- Probe call succeeds before marking authenticated.
- Password never written to `Preferences`.
- Logout clears context and navigates to `/login`.

## Migration To New Pattern
Remove / delete after verifying no references:
- `ServerConfigurationService`
- Mobile `AuthenticationHeaderHandler`
- Any code directly setting `_httpClient.BaseAddress` at runtime

## Future Enhancements
- Use `SecureStorage` for optional password persistence.
- Add `/api/health` light probe to replace `GetTags` for login validation.
- Add offline caching layer (SQLite) for recent activities.

## Security Notes
- Basic Auth credentials transient in memory only.
- Enforce HTTPS; reject or warn on non-HTTPS input.
- No silent fallback to placeholder hosts.
- Avoid logging raw credentials; only log high-level auth events.

## Common Pitfalls Avoided
| Pitfall | Avoidance Strategy |
|---------|--------------------|
| Reusing mutated `HttpClient` | Always create new via factory per configuration epoch |
| Race conditions on logout | Context clear triggers provider invalidation atomically |
| Leaking password to storage | Store only username + server URL |
| Stale auth header | Handler reads latest context on every request |

## Sample Minimal Registration (Extract)
```csharp
builder.Services.AddSingleton<IApiContext, ApiContext>();
builder.Services.AddTransient<DynamicAuthHandler>();
builder.Services.AddHttpClient("dynamic-api").AddHttpMessageHandler<DynamicAuthHandler>();
builder.Services.AddSingleton<IApiClientProvider, ApiClientProvider>();
// Back-compat injection for existing pages
builder.Services.AddTransient<IActivityApi>(sp => sp.GetRequiredService<IApiClientProvider>().Activity);
```

## Error Handling Guidance
- If `ApiContext.Server` is null, report "Server not configured" instead of attempting calls.
- Catch `ApiException` (401) on probe → show invalid credentials, clear context.
- Network failures → instruct user to verify URL / connectivity; do not clear server unless user edits it.

## Summary
The refactor isolates dynamic server selection and credentials into a context-driven pattern that is safe, testable, and avoids platform `HttpClient` mutation constraints. This ensures reliable login/logout cycles and prepares the mobile client for future secure storage and additional API surfaces.
# Mobile App Implementation Summary

## Completed Features

### 1. ✅ Removed URL Input Bar
- Eliminated the URL entry field and go button from the main page
- WebView now takes full screen space in the Home tab

### 2. ✅ Bottom Navigation with Two Tabs
- **Home Tab**: Contains the WebView displaying the LogMyDay web application
- **Quick Activities Tab**: New functionality for rapid activity creation

### 3. ✅ Quick Activities Functionality
- **Enhanced Button Creation (Blazor Mobile)**: Modern Bootstrap modal interface
  - Tag selection dropdown with auto-population from API
  - Auto-naming with tag title pre-fill
  - Dynamic input types based on tag configuration (Integer, String, Boolean, Date)
  - Client-side and server-side form validation
- **Legacy MAUI Implementation**: Traditional dialog-based button creation
  - Select from available tags (fetched from API)
  - Set custom button names  
  - Configure default values based on tag input types
- **One-Tap Activity Creation**: Buttons instantly create activities via API with predefined values
- **15-Second Cooldown**: Prevents accidental double-taps with visual feedback
- **Button Management**: Add and remove buttons with proper confirmation dialogs

### 4. ✅ Refit API Integration
- Replaced basic HTTP client with type-safe Refit implementation
- Integrated with existing LogMyDay.Shared interfaces and DTOs
- Supports activity creation, tag fetching, and duplicate checking
- Basic authentication handler for API security

### 5. ✅ Data Persistence
- Quick activity button configurations stored locally using Preferences API
- Buttons persist between app sessions
- Automatic state restoration (cooldowns reset on app restart)

### 6. ✅ Mobile-Optimized UI
- **Blazor Mobile (Enhanced)**: 
  - Modern floating action button (FAB) for easy access
  - Bootstrap modal with responsive design
  - Fullscreen modal on small devices 
  - Visual feedback with loading states and error handling
  - Consistent design with main Activities page
- **MAUI (Legacy)**: Touch-friendly button layouts using CollectionView with grid layout
- Responsive design with proper spacing and sizing
- Visual feedback for button states (enabled/disabled)
- Status messages for user feedback
- Confirmation dialogs for destructive actions

## Technical Architecture

### Services
- `ApiService`: Refit-based API communication
- `QuickActivityService`: Button management and persistence
- `BasicAuthHandler`: HTTP authentication

### ViewModels
- `QuickActivitiesViewModel`: MVVM pattern for Quick Activities page

### Models
- `QuickActivityButton`: Configuration data for quick activity buttons

### UI Components
- `HomePage`: WebView container for main app
- `QuickActivitiesPage`: Quick activities management interface
- `MainPage`: TabbedPage container with bottom navigation

## Configuration

### API Endpoints
- Development: `http://localhost:5000`
- Production: `https://logmyday.tadata.cz`

### Authentication
- Basic authentication with configurable credentials
- Currently set to demo/demo for testing

### Button Features
- Support for all tag input types (Integer, String, Boolean, Date)
- Automatic value formatting based on tag configuration
- Visual indication of button state and cooldown status
- Persistent storage with JSON serialization

## User Experience

### Quick Activity Creation Flow
1. Tap "+ Add" button
2. Select tag from available options
3. Enter custom button name
4. Set value if required by tag type
5. Button appears in grid layout

### Quick Activity Usage Flow
1. Tap quick activity button
2. Activity is instantly created via API
3. Button shows success message and becomes disabled
4. 15-second cooldown prevents further clicks
5. Button re-enables automatically

### Button Management
- Visual delete button ("✕") on each quick activity
- Confirmation dialog before deletion
- Real-time UI updates when buttons are added/removed

## Future Enhancements Ready
- Login screen for configurable authentication
- Custom icons for better visual appeal
- Offline support with sync when online
- Export/import button configurations
- Enhanced error handling and retry mechanisms
