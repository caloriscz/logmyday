# Loading States Identification Guide

## Where "Loading..." Can Appear

There are **THREE different loading states** in the mobile app:

### 1. Authentication Loading Screen (MainLayout)
**Location**: `LogMyDay.App.Mobile/Components/Layout/MainLayout.razor`

**When it appears**:
- App startup (very brief, during `OnInitializedAsync`)
- Session restoration check (checking stored credentials)

**What you see**:
```
Full-screen dark gradient background
Animated spinner
Message: "Checking authentication..."
Sub-message: "Please wait while we verify your session"
```

**Component**: `<LoadingScreen Message="Checking authentication..." SubMessage="..." />`

**How to test**:
1. Close the app completely
2. Reopen the app
3. Watch for the full-screen loading overlay
4. **Note**: This appears for < 1 second usually, very hard to catch!

**Problem**: The `_isCheckingAuthentication` flag might be set to false too quickly:
```csharp
// MainLayout.razor - Line ~117
_isCheckingAuthentication = true;
StateHasChanged();

await TryRestoreSessionAsync();  // This might be instant if already authenticated
await HandlePendingNotificationAsync();

_isCheckingAuthentication = false;  // Immediately set to false!
StateHasChanged();
```

**To make it visible for testing**, add a delay:
```csharp
await TryRestoreSessionAsync();
await Task.Delay(2000); // ADD THIS LINE - 2 second delay to see the loading screen
await HandlePendingNotificationAsync();
```

---

### 2. Activities Page Loading (Activities.razor)
**Location**: `LogMyDay.App.Mobile/Components/Pages/Activities.razor` - Line 60

**When it appears**:
- First time loading the Activities page
- Waiting for API response with activity data
- After filters change, waiting for new data

**What you see**:
```
Gray text: "Loading..."
```

**Code**:
```razor
@if (pagedResult == null)
{
    <div class="text-gray-600 dark:text-gray-400">
        <em>Loading...</em>
    </div>
}
```

**How to test**:
1. Navigate to Activities page
2. Should appear briefly while fetching data from API
3. More visible if API is slow or you have slow network

---

### 3. Tags Page Loading (Tags.razor)
**Location**: `LogMyDay.App.Mobile/Components/Pages/Tags.razor` - Line 21

**When it appears**:
- First time loading the Tags page
- Waiting for API response with tag data

**What you see**:
```
Gray text: "Loading..."
```

**Code**:
```razor
@if (tags == null)
{
    <p class="text-gray-600 dark:text-gray-400"><em>Loading...</em></p>
}
```

**How to test**:
1. Navigate to Tags page
2. Should appear briefly while fetching data from API

---

## Distinguishing Between Loading States

### Authentication Loading (Full Screen)
- **Appearance**: Full-screen dark overlay
- **Position**: Covers entire app, centered
- **Spinner**: Large animated spinner
- **Message**: Multi-line with title and subtitle
- **Duration**: < 1 second (usually too fast to see)
- **When**: Only during app startup/resume

### Page Loading (Inline)
- **Appearance**: Small text in content area
- **Position**: Where the data list would appear
- **Spinner**: None (just text)
- **Message**: Simple "Loading..."
- **Duration**: Depends on API response time
- **When**: Every time you navigate to Activities or Tags page

---

## Testing Each Loading State

### Test 1: Authentication Loading Screen
```csharp
// Add to MainLayout.razor OnInitializedAsync (around line 125)
await TryRestoreSessionAsync();
await Task.Delay(3000); // 3 seconds - TESTING ONLY
await HandlePendingNotificationAsync();
```

**Expected**: You should see full-screen dark overlay with "Checking authentication..."

### Test 2: Activities Page Loading
1. Clear app data or log out
2. Log back in
3. Navigate to Activities page
4. Watch for "Loading..." text in gray

**Expected**: Gray "Loading..." text where activities list will appear

### Test 3: Tags Page Loading
1. Navigate to Tags page from another page
2. Watch for "Loading..." text in gray

**Expected**: Gray "Loading..." text where tags list will appear

---

## Current Status

### ✅ Fixed
- Login page now uses `login-dark-bg` class (always dark, `!important` overrides)
- LoadingScreen component has empty default message (will use passed-in message)
- LoadingScreen has proper z-index (z-[70])

### ❓ To Identify
Which "Loading..." are you seeing?
1. Full-screen with spinner? → Authentication loading
2. Gray text in content area? → Activities/Tags page loading

### 🔧 Quick Test Addition

Add this to see authentication loading screen:

**File**: `LogMyDay.App.Mobile/Components/Layout/MainLayout.razor`
**Line**: ~125 (in `OnInitializedAsync`)

```csharp
await TryRestoreSessionAsync();
#if DEBUG
await Task.Delay(2000); // Temporary delay to see loading screen
#endif
await HandlePendingNotificationAsync();
```

This will make the loading screen visible for 2 seconds in debug builds only.

---

## Z-Index Issue Resolution

If FAB button still shows through modals:

1. **Verify Tailwind CSS was rebuilt**:
   ```powershell
   cd ui
   npx tailwindcss -i ./src/css/tailwind.css -o ../LogMyDay.App.Mobile/wwwroot/css/tailwind.css --minify
   ```

2. **Check generated CSS contains**:
   - `.z-\[60\]{z-index:60}`
   - `.z-\[70\]{z-index:70}`

3. **Verify modal elements have `z-[60]` class** in HTML (browser dev tools)

4. **Check FAB has `z-40`** (defined in app.css)

---

## Summary

**Login Page**: Now uses `login-dark-bg` class with `!important` - should always be dark

**Authentication Loading**: Add delay to make it visible for testing

**Page Loading**: Already visible when API is slow

Let me know which "Loading..." you're seeing and I can help you modify it!
