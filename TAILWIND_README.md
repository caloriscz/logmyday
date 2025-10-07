# LogMyDay - Bootstrap to Tailwind CSS Migration

## 🎯 Migration Overview

This repository is undergoing a migration from Bootstrap 5 to Tailwind CSS for both the web app (Blazor Server) and mobile app (MAUI Blazor Hybrid). The build infrastructure and theme system are complete, with component migration in progress.

## 📁 New Files & Directories

### Build System
- `ui/` - Vite + Tailwind build workspace
  - `src/css/tailwind.css` - Tailwind directives and custom components
  - `src/js/app.js` - Theme toggle and utilities
  - `dist/` - Build output (CSS & JS)
  - `package.json`, `vite.config.js`, `tailwind.config.js`, `postcss.config.js`

### Components
- `LogMyDay.UI/Components/Icons/Icon.razor` - Heroicons component (30+ icons)
- `LogMyDay.UI/Components/ThemeToggle.razor` - Dark/light mode toggle button

### Documentation
- `MIGRATION_STATUS.md` - **START HERE** - Current progress and next steps
- `TAILWIND_MIGRATION.md` - Comprehensive migration guide with patterns
- `TAILWIND_QUICK_REFERENCE.md` - Bootstrap → Tailwind class mappings
- `ui/README.md` - UI build system documentation

### Scripts
- `build-tailwind.ps1` - PowerShell build script with helpful commands

## 🚀 Quick Start

### Prerequisites
- Node.js 16+ (for Vite and npm)
- .NET 9 SDK
- Visual Studio 2022 or VS Code

### 1. Install UI Dependencies

```powershell
cd ui
npm install
```

### 2. Build Tailwind CSS

```powershell
npm run build
```

Output:
- `ui/dist/css/tailwind.css` (~30KB, ~5KB gzipped)
- `ui/dist/js/app.js` (~1.3KB, ~0.6KB gzipped)

### 3. Build & Run Web App

The MSBuild integration automatically builds UI assets:

```powershell
dotnet run --project LogMyDay.App/LogMyDay.App.csproj
```

### 4. Build MAUI Mobile App

```powershell
dotnet build LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj
```

## 🎨 Using the Build Script

```powershell
# Build Tailwind CSS only
.\build-tailwind.ps1

# Watch mode (auto-rebuild on changes)
.\build-tailwind.ps1 -Watch

# Build and run web app
.\build-tailwind.ps1 -BuildWeb -RunWeb

# Build mobile app
.\build-tailwind.ps1 -BuildMobile

# Clean and rebuild everything
.\build-tailwind.ps1 -Clean
```

## 📊 Migration Status

| Component | Status |
|-----------|--------|
| Build Infrastructure | ✅ Complete |
| Theme System (Dark/Light) | ✅ Complete |
| Icon System (Heroicons) | ✅ Complete |
| MSBuild Integration | ✅ Complete |
| HTML Head Updates | ✅ Complete |
| Documentation | ✅ Complete |
| Component Migration | ⏸️ Not Started |
| Date Picker Migration | ⏸️ Not Started |
| Testing | ⏸️ Not Started |

**Overall Progress**: ~60% (Infrastructure complete, components pending)

See `MIGRATION_STATUS.md` for detailed breakdown.

## 🔧 What's Changed

### Removed
- ❌ Bootstrap 5 CSS & JS (CDN links removed)
- ❌ Bootstrap Icons (CDN link removed)
- ❌ Flatpickr date picker library (CDN links removed)

### Added
- ✅ Tailwind CSS 3.4+ (local build, no CDN)
- ✅ Heroicons (inline SVG components)
- ✅ Native HTML5 date/time inputs (Flatpickr replacement)
- ✅ Dark/light theme toggle system
- ✅ Vite build pipeline
- ✅ Custom Tailwind component classes

### Modified
- 📝 `LogMyDay.App/Components/App.razor` - Updated head/scripts
- 📝 `LogMyDay.App.Mobile/wwwroot/index.html` - Updated head/scripts
- 📝 `LogMyDay.App/LogMyDay.App.csproj` - Added MSBuild targets
- 📝 `LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj` - Added MSBuild targets
- 📝 `LogMyDay.UI/_Imports.razor` - Added Icons namespace

## 🎨 Theme System

### How It Works
1. User preference stored in localStorage (`lmd-theme`)
2. FOUC prevention script in HTML `<head>` applies theme before page loads
3. Tailwind's `class` dark mode strategy (toggles `.dark` class on `<html>`)
4. System preference detection (`prefers-color-scheme: dark`)

### Using the Theme Toggle

```razor
@* Add to your layout *@
<ThemeToggle />
```

### JavaScript API

```javascript
// Get current theme
let theme = window.LogMyDayTheme.get(); // 'light' or 'dark'

// Set theme
window.LogMyDayTheme.set('dark');

// Toggle theme
window.LogMyDayTheme.toggle();
```

## 🖼️ Icon System

### Usage

```razor
@* In your Razor component *@
<Icon Name="home" Class="w-5 h-5" />
<Icon Name="plus" Class="w-6 h-6 text-primary-600" />
<Icon Name="trash" Class="w-4 h-4 text-danger-600" />
```

### Available Icons
home, plus, tag, list, bell, database, chart, user, calendar, clock, edit, trash, close, check, chevron-left, chevron-right, chevron-down, chevron-up, menu, search, settings, download, upload, info, warning, sun, moon, filter, note, and more...

See `Icon.razor` for full list or add new ones from https://heroicons.com/

## 🎨 Custom Tailwind Classes

Pre-defined component classes (in `ui/src/css/tailwind.css`):

### Buttons
```html
<button class="btn-primary">Primary</button>
<button class="btn-secondary btn-sm">Small</button>
<button class="btn-danger btn-lg">Large Danger</button>
```

### Forms
```html
<label class="form-label">Name</label>
<input type="text" class="form-input" />
<select class="form-select">...</select>
<input type="checkbox" class="form-checkbox" />
<span class="form-error">Error message</span>
<span class="form-hint">Help text</span>
```

### Cards
```html
<div class="card">
  <div class="card-header">Header</div>
  <div class="card-body">Content</div>
  <div class="card-footer">Footer</div>
</div>
```

### Alerts
```html
<div class="alert-info">Information</div>
<div class="alert-success">Success</div>
<div class="alert-warning">Warning</div>
<div class="alert-danger">Danger</div>
```

### Badges
```html
<span class="badge-primary">Primary</span>
<span class="badge-success">Success</span>
<span class="badge-danger">Danger</span>
```

## 📝 Next Steps (Component Migration)

### Immediate Priority
1. Convert `MainLayout.razor` (web & mobile)
2. Convert `NavMenu.razor`
3. Test basic navigation and theme toggle

### Short Term
1. Convert Login page
2. Convert Home page
3. Convert one feature page at a time
4. Replace Flatpickr with native date inputs

### Reference
- See `TAILWIND_QUICK_REFERENCE.md` for Bootstrap → Tailwind class mappings
- See `TAILWIND_MIGRATION.md` for detailed patterns and examples
- See `MIGRATION_STATUS.md` for current progress

## 🧪 Testing

### Before Converting a Component
1. Take a screenshot of the current rendering
2. Note all interactive behaviors (modals, dropdowns, etc.)

### After Converting a Component
1. Visual comparison with screenshot
2. Test all interactive elements
3. Test in light and dark themes
4. Test responsive behavior (mobile/desktop)
5. Check browser console for errors

### Test Checklist
- [ ] Theme toggle works and persists
- [ ] All icons display correctly
- [ ] Forms validate properly
- [ ] Modals open/close
- [ ] Navigation works
- [ ] Mobile responsive
- [ ] No Bootstrap classes in DOM
- [ ] No console errors

## 🆘 Troubleshooting

### Build Errors

**"npm not found"**
- Install Node.js from https://nodejs.org/

**"Cannot find module 'vite'"**
- Run `cd ui && npm install`

**"Build failed"**
- Check `ui/package.json` dependencies
- Delete `ui/node_modules` and `npm install` again

### Styling Issues

**"Styles not applying"**
- Rebuild: `cd ui && npm run build`
- Check that your files are in Tailwind's `content` paths (see `tailwind.config.js`)
- Clear browser cache (Ctrl+F5)

**"Dark mode not working"**
- Check that FOUC prevention script is in HTML `<head>`
- Verify `dark` class is on `<html>` element (DevTools)
- Check localStorage key `lmd-theme` (Application tab in DevTools)

**"CSS file too large"**
- Ensure Tailwind is purging unused classes (check `content` paths in `tailwind.config.js`)
- Production build should be ~30KB (~5KB gzipped)

### Icon Issues

**"Icon not found"**
- Check that icon name exists in `Icon.razor`
- Add new icons from https://heroicons.com/ if needed
- Ensure SVG uses `stroke="currentColor"` or `fill="currentColor"`

## 📚 Documentation

- **`MIGRATION_STATUS.md`** - Current progress, completion %, what's left
- **`TAILWIND_MIGRATION.md`** - Detailed migration guide with patterns
- **`TAILWIND_QUICK_REFERENCE.md`** - Quick class lookup table
- **`ui/README.md`** - UI build system details
- **`build-tailwind.ps1`** - Automated build script

## 🤝 Contributing to Migration

When converting a component:

1. **Read the docs first**
   - Check `TAILWIND_QUICK_REFERENCE.md` for class mappings
   - Review patterns in `TAILWIND_MIGRATION.md`

2. **Convert incrementally**
   - Don't convert everything at once
   - Test after each component/page

3. **Maintain accessibility**
   - Keep ARIA labels
   - Ensure focus states are visible
   - Test keyboard navigation

4. **Support dark mode**
   - Always include `dark:` variants for colors
   - Test in both themes

5. **Update documentation**
   - Mark items as complete in `MIGRATION_STATUS.md`
   - Add any new patterns to `TAILWIND_MIGRATION.md`

## 📄 License

This project is licensed under [Your License Here].

---

**Migration Started**: October 7, 2025  
**Current Status**: Phase 1 Complete (Infrastructure), Phase 2 Ready (Component Conversion)  
**Questions?** See `MIGRATION_STATUS.md` or open an issue.
