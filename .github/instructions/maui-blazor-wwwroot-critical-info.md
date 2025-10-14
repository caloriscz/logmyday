# MAUI Blazor wwwroot - Critical Undocumented Behavior

## 🚨 CRITICAL: How MAUI Blazor Actually Loads wwwroot Files

### The Hidden Truth (Not Documented by Microsoft)

**MAUI Blazor wwwroot behavior is complex and poorly documented!**

The SDK build tasks (hidden inside MAUI internals):
1. Scan all referenced projects for `wwwroot` folders
2. Merge/overlay them into the app's assets
3. **If multiple projects have the same file**, precedence rules apply (library vs app)
4. **If only one project has a file**, that file is used

### LogMyDay's Actual Architecture

```
LogMyDay.sln
├── LogMyDay.UI/              # Shared Razor components
│   └── wwwroot/
│       └── js/               ✅ Shared JavaScript modules
│           ├── hiit-timer.js
│           └── breathing.js
│
├── LogMyDay.App/             # Blazor Server app
│   └── wwwroot/
│       ├── index.html        ✅ Server entry point
│       └── app.css           # Server-specific styles
│
└── LogMyDay.App.Mobile/      # MAUI Blazor mobile
    └── wwwroot/
        ├── index.html        ✅ Mobile entry point (THE ONE ACTUALLY USED!)
        ├── app.css           ✅ Mobile-specific styles
        ├── css/
        │   └── tailwind.css  ✅ Built from ui/dist (CopyTailwindAssets)
        └── js/               ✅ Mobile-specific scripts
```

### Key Insight: LogMyDay Uses Separate index.html Files

Unlike some MAUI setups, LogMyDay has:
- ❌ **NO** `LogMyDay.UI/wwwroot/index.html` (no shared HTML entry point)
- ✅ `LogMyDay.App/wwwroot/index.html` for Blazor Server
- ✅ `LogMyDay.App.Mobile/wwwroot/index.html` for MAUI mobile

**Why?** Each platform (Server vs Mobile) has different requirements:
- Server: Runs in browser, standard web setup
- Mobile: Runs in BlazorWebView, needs mobile-specific meta tags, FOUC prevention, etc.

### The Configuration That Makes It Work

**File**: `LogMyDay.UI/wwwroot/index.html` (THE REAL ONE USED)

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover" />
    <title>LogMyDay</title>
    <base href="/" />
    
    <!-- Tailwind CSS from shared UI library -->
    <link href="css/tailwind.css" rel="stylesheet" />
    
    <!-- Mobile-specific styles from mobile project -->
    <link href="app.css" rel="stylesheet" />
    
    <!-- Other shared assets... -->
</head>
<body>
    <!-- App content -->
</body>
</html>
```

### Why This Is Badly Documented

Microsoft's official documentation:
- ❌ Doesn't explain the wwwroot merge/overlay behavior clearly
- ❌ Doesn't mention how referenced projects' wwwroot folders are combined
- ❌ Doesn't clarify file precedence rules
- ❌ Doesn't document the SDK build task internals
- ❌ Doesn't provide examples of multi-project wwwroot setups

**Result**: Developers waste hours debugging why CSS/JS files aren't loading!

### The Solution for LogMyDay

#### Where to Put Files:

| File Type | Location | Reason |
|-----------|----------|--------|
| **Mobile index.html** | `LogMyDay.App.Mobile/wwwroot/` | Mobile-specific entry point |
| **Mobile CSS** | `LogMyDay.App.Mobile/wwwroot/app.css` | Mobile-specific custom styles |
| **Tailwind CSS** | Built to `LogMyDay.App.Mobile/wwwroot/css/` | Via CopyTailwindAssets from ui/dist |
| **Shared JS modules** | `LogMyDay.UI/wwwroot/js/` | Shared across Server + Mobile |
| **Mobile JS** | `LogMyDay.App.Mobile/wwwroot/js/` | Mobile-specific scripts |

#### Critical: The index.html You Edit

**File**: `LogMyDay.App.Mobile/wwwroot/index.html` ✅ THIS IS THE ONE!

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover" />
    <title>LogMyDay Mobile</title>
    <base href="/" />
    
    <!-- Tailwind CSS (built from ui/dist via CopyTailwindAssets target) -->
    <link href="css/tailwind.css" rel="stylesheet" />
    
    <!-- Mobile-specific custom styles -->
    <link href="app.css" rel="stylesheet" />
    
    <!-- Theme initialization script -->
    <script>
        (function() {
            const theme = localStorage.getItem('lmd-theme') || 
                (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
            document.documentElement.setAttribute('data-bs-theme', theme);
            if (theme === 'dark') {
                document.documentElement.classList.add('dark');
            }
        })();
    </script>
</head>
<body>
    <div id="app"><!-- Blazor app loads here --></div>
    <script src="_framework/blazor.webview.js"></script>
</body>
</html>
```

**Important**: Changes to this file require rebuild + redeploy to take effect!

### The MauiAsset Configuration

**File**: `LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj`

```xml
<ItemGroup>
    <!-- Include mobile-specific wwwroot files -->
    <MauiAsset Include="wwwroot\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

This ensures mobile-specific files (like `app.css`) are included in the APK.

### Build Process Flow

1. **CopyTailwindAssets** target: `ui/dist/*` → `LogMyDay.UI/wwwroot/css/`
2. **SDK asset resolution**: Merges all referenced project wwwroot folders
3. **Precedence**: Library wwwroot (LogMyDay.UI) + Mobile wwwroot overlay
4. **APK packaging**: Bundles merged assets into Android APK
5. **Runtime**: BlazorWebView loads `index.html` from merged assets

### Debugging Tips

#### To Find Which index.html Is Used:
```powershell
# After build, check the actual file in Android assets:
Get-Content "LogMyDay.App.Mobile\obj\Debug\net9.0-android\assets\index.html"
```

#### To Verify CSS Files Are Included:
```powershell
# Check Android assets folder:
Get-ChildItem "LogMyDay.App.Mobile\obj\Debug\net9.0-android\assets\" -Recurse -Filter "*.css"

# Should show:
# - tailwind.css (from LogMyDay.UI)
# - app.css (from LogMyDay.App.Mobile)
```

#### To See Asset Merge Process:
```powershell
# Build with detailed logging:
dotnet build LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj -f net9.0-android -v detailed
```

### Common Pitfalls

❌ **Mistake**: Editing `LogMyDay.App.Mobile/wwwroot/index.html` expecting it to be used
✅ **Solution**: Edit `LogMyDay.UI/wwwroot/index.html` instead

❌ **Mistake**: Adding CSS links to mobile index.html that gets ignored
✅ **Solution**: Add CSS links to the shared `LogMyDay.UI/wwwroot/index.html`

❌ **Mistake**: Not including mobile-specific CSS in mobile wwwroot
✅ **Solution**: Keep mobile-specific files in mobile wwwroot, reference them in shared index.html

❌ **Mistake**: Forgetting `<MauiAsset Include="wwwroot\**" />` in .csproj
✅ **Solution**: Always include this to ensure mobile wwwroot files are packaged

### Testing After Changes

1. **Clean build**: `dotnet clean`
2. **Rebuild**: `dotnet build -f net9.0-android`
3. **Verify assets**: Check `obj/Debug/net9.0-android/assets/` folder
4. **Uninstall old app**: Remove from device to clear cache
5. **Deploy**: Fresh installation

---

## ✅ SUCCESS: Visual Confirmation

**Screenshot Date**: October 14, 2025

The Activities page now shows:
- ✅ **Cards with visible styling** - White/light gray backgrounds with borders
- ✅ **Proper card layout** - Activity items display in card format
- ✅ **Delete buttons** - Red danger buttons visible on each card
- ✅ **Date navigation** - Date picker and navigation arrows working
- ✅ **Filter button** - Secondary button with proper gray styling
- ✅ **Ascending/Descending links** - Proper text styling
- ✅ **FAB button** - Blue floating action button at bottom right

### What Fixed It:

1. ✅ Added `<MauiAsset Include="wwwroot\**" />` to `.csproj`
2. ✅ Ensured `index.html` references both CSS files:
   - `<link href="css/tailwind.css" rel="stylesheet" />`
   - `<link href="app.css" rel="stylesheet" />`
3. ✅ Clean build + uninstall + redeploy

### The Cards Are Working!

From the screenshot, activity cards show:
- **Card title**: Activity tag name (e.g., "foodscore", "sleep support")
- **Value**: Activity value (e.g., "1", "3", "800", "0")
- **Timestamps**: "Started: 17:29" and "Finished: -"
- **Delete button**: Red trash icon button on right
- **Borders**: Visible card borders separating each activity
- **Background**: Proper white/gray card backgrounds

---

## Real-World Example: LogMyDay Setup

### Project Structure
```
LogMyDay.sln
├── LogMyDay.UI/              # Shared Razor components + wwwroot
│   └── wwwroot/
│       ├── index.html        ✅ Used by mobile app
│       └── css/
│           └── tailwind.css  ✅ Shared styles
│
├── LogMyDay.App/             # Blazor Server app
│   └── wwwroot/
│       └── app.css           # Server-specific styles
│
└── LogMyDay.App.Mobile/      # MAUI Blazor mobile
    └── wwwroot/
        └── app.css           ✅ Mobile-specific styles
```

### The Working Configuration

**`LogMyDay.UI/wwwroot/index.html`** (shared):
```html
<link href="css/tailwind.css" rel="stylesheet" />  <!-- From LogMyDay.UI -->
<link href="app.css" rel="stylesheet" />           <!-- From mobile wwwroot -->
```

**Result**:
- ✅ Tailwind CSS loads from `LogMyDay.UI/wwwroot/css/tailwind.css`
- ✅ Mobile custom styles load from `LogMyDay.App.Mobile/wwwroot/app.css`
- ✅ Both apps (Server + Mobile) share the same index.html base
- ✅ Each app can have project-specific `app.css` customizations

This setup allows:
- 🎨 Shared design system (Tailwind) across all apps
- 🎨 Platform-specific customizations (app.css per project)
- 🎨 Single source of truth for HTML structure
- 🎨 Easy maintenance and consistency
