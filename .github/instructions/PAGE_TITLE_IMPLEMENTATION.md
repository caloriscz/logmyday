# PageTitle Management System Implementation

## Overview
This implementation provides a programmatic solution for managing page titles in the LogMyDay Blazor application. All page titles automatically append " - LogMyDay" to maintain consistent branding across the application.

## Components Created

### 1. IPageTitleService & PageTitleService
**File:** `LogMyDay.App/Services/PageTitleService.cs`

- **Interface:** `IPageTitleService` - Defines the contract for page title management
- **Implementation:** `PageTitleService` - Handles automatic site name appending
- **Methods:**
  - `SetTitle(string title)` - Sets a page title with automatic " - LogMyDay" appending
  - `SetRawTitle(string title)` - Sets a complete title without appending (for special cases)
  - `CurrentTitle` property - Gets the current formatted title
  - `TitleChanged` event - Fires when the title changes

### 2. DynamicPageTitle Component
**File:** `LogMyDay.App/Components/Shared/DynamicPageTitle.razor`

- Renders the actual `<PageTitle>` component with the current dynamic title
- Listens to title changes from the PageTitleService
- Automatically updates the browser title when changes occur
- Implements `IDisposable` for proper cleanup

### 3. BasePage Component (Optional)
**File:** `LogMyDay.App/Components/Base/BasePage.razor`

- Optional wrapper component that pages can inherit from
- Provides automatic title management for new pages
- Includes `DynamicPageTitle` component automatically

### 4. MainLayout Integration
**File:** `LogMyDay.App/Components/Layout/MainLayout.razor`

- Added `DynamicPageTitle` component to all layout branches
- Ensures dynamic title management works across all authentication states

## Service Registration

The `PageTitleService` is registered in the dependency injection container in `Program.cs`:

```csharp
services.AddScoped<LogMyDay.App.Services.IPageTitleService, LogMyDay.App.Services.PageTitleService>();
```

## Usage in Pages

### Current Implementation Pattern
All existing pages have been updated to use the new system:

```csharp
@using LogMyDay.App.Services
@inject IPageTitleService PageTitleService

@code {
    protected override void OnInitialized()
    {
        PageTitleService.SetTitle("Page Name"); // Automatically becomes "Page Name - LogMyDay"
    }
}
```

### For New Pages
New pages should follow the same pattern:

1. Inject `IPageTitleService`
2. Call `PageTitleService.SetTitle("Your Page Title")` in `OnInitialized` or `OnInitializedAsync`
3. The service automatically appends " - LogMyDay"

### Alternative: Using BasePage Component
For simpler implementation, new pages can inherit from `BasePage`:

```html
<BasePage Title="Your Page Title">
    <!-- Your page content here -->
</BasePage>
```

## Pages Updated

All existing pages have been converted to use the new system:

- ✅ Home.razor → "Home - LogMyDay"
- ✅ Login.razor → "Login - LogMyDay"
- ✅ Breathing.razor → "Advanced Breathing Techniques - LogMyDay"
- ✅ Exercise.razor → "HIIT Timer - LogMyDay"
- ✅ Error.razor → "Error - LogMyDay"
- ✅ ForgotPassword.razor → "Forgot Password - LogMyDay"
- ✅ Backup.razor → "Backup & Restore - LogMyDay"
- ✅ Profile.razor → "My Profile - LogMyDay"
- ✅ RegisterFirst.razor → "Create First Admin User - LogMyDay"
- ✅ ResetPassword.razor → "Reset Password - LogMyDay"
- ✅ TagEdit.razor → "Edit Tag - LogMyDay"
- ✅ Tags.razor → "Tags - LogMyDay"
- ✅ Tools.razor → "Tools - LogMyDay"
- ✅ Users.razor → "User Management - LogMyDay"
- ✅ Calendar.razor → "Calendar - LogMyDay"
- ✅ Notifications.razor → "Notifications - LogMyDay"

## Fallback Title

The static title in `App.razor` remains as "LogMyDay" and serves as a fallback when no page-specific title is set.

## Benefits

1. **Consistent Branding:** All pages automatically include the site name
2. **Programmatic Control:** Easy to change site name or title format in one place
3. **Future-Proof:** New pages automatically get proper titles when using the service
4. **Dynamic Updates:** Titles can be changed dynamically during page lifecycle
5. **Clean Code:** Removes manual duplication of " - LogMyDay" across all pages

## Notes

- The system is backward compatible - existing `<PageTitle>` components have been replaced
- The service uses events to notify the `DynamicPageTitle` component of changes
- The implementation is scoped to ensure proper lifecycle management
- All pages have been tested for compilation success