# Localization Quick Reference Guide

## For Developers: Using Localization Features

### Date/Time Input Controls

When adding date or time input controls in Razor components, always include the `lang` attribute:

```razor
<input type="date" @bind="myDate" class="form-control" lang="@InputLanguage" />
<input type="time" @bind="myTime" class="form-control" lang="@InputLanguage" />
<input type="datetime-local" @bind="myDateTime" class="form-control" lang="@InputLanguage" />
```

Where `InputLanguage` is typically defined as:
```csharp
private string InputLanguage => displayCulture.Name;
```

### Loading User Preferences

In your component's code section:

```csharp
@inject IUserPreferencesService UserPreferencesService

// Fields
private UserPreferencesSnapshot? userPreferences;
private CultureInfo displayCulture = CultureInfo.CurrentCulture;
private TimeZoneInfo displayTimeZone = TimeZoneInfo.Local;

// In OnInitializedAsync
protected override async Task OnInitializedAsync()
{
    userPreferences = await UserPreferencesService.GetAsync();
    displayCulture = userPreferences.Culture;
    displayTimeZone = userPreferences.TimeZone;
    
    // ... rest of initialization
}
```

### Timezone-Aware Date Operations

Always use timezone-aware methods when working with dates:

```csharp
// Get current time in user's timezone
private DateTime GetCurrentDateTimeInDisplayZone()
    => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, displayTimeZone);

// Get today's date in user's timezone
private DateTime GetTodayInDisplayZone() 
    => GetCurrentDateTimeInDisplayZone().Date;

// Smart date initialization for activities
var today = GetTodayInDisplayZone();
Activity.DateStarted = selectedDate.Date == today
    ? GetCurrentDateTimeInDisplayZone()  // Use current time for today
    : selectedDate.Date;                 // Use midnight for other dates
```

### Passing Culture to Child Components

When using `AddActivityModal` or similar localized components:

```razor
<AddActivityModal
    Activity="newActivity"
    AvailableTags="tags"
    OptionLists="optionLists"
    OptionListLookup="optionListLookup"
    DisplayCulture="displayCulture"
    DisplayTimeZone="displayTimeZone"
    OnActivityCreated="OnActivityCreated"
    OnError="OnError" />
```

### Option List Integration

Load and maintain option list lookup dictionary:

```csharp
private IList<TagOptionListResponse>? optionLists;
private Dictionary<int, TagOptionListResponse>? optionListLookup;

// Load option lists
optionLists = await ActivityApi.GetTagOptionListsAsync();

// Create lookup dictionary
if (optionLists is not null)
{
    optionListLookup = optionLists.ToDictionary(list => list.Id);
}
```

### Formatting Dates for Display

Use user preferences for date/time formatting:

```csharp
private EffectivePreferences? effectivePreferences;

private string FormatDate(DateTime date)
{
    var pattern = effectivePreferences?.ShortDatePattern 
        ?? displayCulture.DateTimeFormat.ShortDatePattern;
    return date.ToString(pattern, displayCulture);
}

private string FormatTime(DateTime dateTime)
{
    var pattern = effectivePreferences?.ShortTimePattern 
        ?? displayCulture.DateTimeFormat.ShortTimePattern;
    return dateTime.ToString(pattern, displayCulture);
}
```

### Number Formatting for Tags

When displaying numeric values from tags:

```csharp
// Integer values
if (tag?.TypeId == 1 && long.TryParse(rawValue, NumberStyles.Integer, 
    CultureInfo.InvariantCulture, out var intValue))
{
    valueForDisplay = intValue.ToString(displayCulture);
}

// Decimal values
if (tag?.TypeId == 6 && decimal.TryParse(rawValue, NumberStyles.Float, 
    CultureInfo.InvariantCulture, out var decimalValue))
{
    valueForDisplay = decimalValue.ToString("0.#############################", displayCulture);
}
```

### Cache Invalidation

If you need to force a preference refresh (e.g., after user updates settings):

```csharp
@inject IUserPreferencesService UserPreferencesService

// Invalidate cache
UserPreferencesService.InvalidateCache();

// Reload preferences
userPreferences = await UserPreferencesService.GetAsync();
displayCulture = userPreferences.Culture;
displayTimeZone = userPreferences.TimeZone;
```

### Subscribe to Preference Changes

To react to preference changes:

```csharp
protected override async Task OnInitializedAsync()
{
    // Load initial preferences
    await LoadPreferences();
    
    // Subscribe to changes
    UserPreferencesService.PreferencesChanged += OnPreferencesChanged;
}

private async void OnPreferencesChanged(object? sender, EventArgs e)
{
    await InvokeAsync(async () =>
    {
        await LoadPreferences();
        StateHasChanged();
    });
}

public void Dispose()
{
    UserPreferencesService.PreferencesChanged -= OnPreferencesChanged;
}
```

## Common Patterns

### Page Initialization Pattern

```csharp
protected override async Task OnInitializedAsync()
{
    // 1. Load user preferences
    userPreferences = await UserPreferencesService.GetAsync();
    displayCulture = userPreferences.Culture;
    displayTimeZone = userPreferences.TimeZone;
    effectivePreferences = userPreferences.Preferences;
    
    // 2. Initialize date fields with timezone awareness
    var today = GetTodayInDisplayZone();
    selectedDate = today;
    
    // 3. Initialize new activity with smart date
    newActivity = new ActivityRequest
    {
        DateStarted = GetCurrentDateTimeInDisplayZone()
    };
    
    // 4. Load page data
    await LoadData();
}
```

### Data Loading Pattern

```csharp
private async Task LoadData()
{
    try
    {
        // Always refresh metadata to ensure latest data
        tags = await ActivityApi.GetTags();
        optionLists = await ActivityApi.GetTagOptionListsAsync();
        
        if (optionLists is not null)
        {
            optionListLookup = optionLists.ToDictionary(list => list.Id);
        }
        
        // Load page-specific data...
    }
    catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    {
        Navigation.NavigateTo("/login");
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error loading data");
    }
}
```

## Anti-Patterns to Avoid

❌ **DON'T** use `DateTime.Now` or `DateTime.Today` directly:
```csharp
// BAD
var today = DateTime.Today;
Activity.DateStarted = DateTime.Now;
```

✅ **DO** use timezone-aware methods:
```csharp
// GOOD
var today = GetTodayInDisplayZone();
Activity.DateStarted = GetCurrentDateTimeInDisplayZone();
```

---

❌ **DON'T** cache option lists without refreshing:
```csharp
// BAD
if (optionLists is null)
{
    optionLists = await ActivityApi.GetTagOptionListsAsync();
}
```

✅ **DO** always refresh metadata:
```csharp
// GOOD
optionLists = await ActivityApi.GetTagOptionListsAsync();
if (optionLists is not null)
{
    optionListLookup = optionLists.ToDictionary(list => list.Id);
}
```

---

❌ **DON'T** forget the `lang` attribute on date inputs:
```csharp
// BAD
<input type="date" @bind="myDate" class="form-control" />
```

✅ **DO** include localization support:
```csharp
// GOOD
<input type="date" @bind="myDate" class="form-control" lang="@InputLanguage" />
```

## Testing Checklist

When implementing localization features, verify:

- [ ] Date inputs display in user's preferred format
- [ ] Time inputs respect 12/24 hour preferences
- [ ] "Today" comparisons account for timezone differences
- [ ] Activity timestamps are created in user's timezone
- [ ] Option lists show display names, not raw values
- [ ] Number inputs respect decimal separators
- [ ] Calendar controls use correct start of week
- [ ] Date formatting follows user's culture patterns
- [ ] Cache invalidation works when preferences change
- [ ] Metadata (tags, option lists) refresh on page load

## Resources

- **User Preferences**: `LogMyDay.Shared.Preferences.EffectivePreferences`
- **Service Interface**: `LogMyDay.App.Services.IUserPreferencesService`
- **Modal Component**: `LogMyDay.App.Components.Shared.AddActivityModal`
- **Instructions**: `.github/instructions/instructions.md`
- **Implementation Summary**: `.github/instructions/localization-implementation-summary.md`
