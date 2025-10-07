# LogMyDay UI Build System

This directory contains the Vite-based build system for Tailwind CSS and JavaScript assets used by both the LogMyDay web app and MAUI mobile app.

## Structure

```
ui/
├── src/
│   ├── css/
│   │   └── tailwind.css      # Tailwind directives and custom components
│   └── js/
│       └── app.js             # Theme toggle and utility functions
├── dist/                      # Build output (generated, not in git)
│   ├── css/
│   │   └── tailwind.css       # Compiled and minified CSS
│   └── js/
│       └── app.js             # Minified JavaScript
├── package.json               # Dependencies
├── vite.config.js             # Vite build configuration
├── tailwind.config.js         # Tailwind customization
├── postcss.config.js          # PostCSS plugins
└── .gitignore
```

## Setup

```bash
# Install dependencies
npm install

# Build for production (one-time)
npm run build

# Watch mode (auto-rebuild on changes)
npm run dev
```

## Integration with .NET Projects

The MSBuild integration automatically:

1. **LogMyDay.App (Web)**: Runs `npm run build` before each build and copies assets to `wwwroot/`
2. **LogMyDay.App.Mobile (MAUI)**: Copies pre-built assets from `ui/dist/` to `wwwroot/`

## Tailwind Configuration

- **Dark Mode**: `class` strategy (toggle via `.dark` class on `<html>`)
- **Content Paths**: Scans `.razor`, `.cshtml`, `.html` files in LogMyDay.App and LogMyDay.App.Mobile
- **Custom Theme**: Extended color palettes (primary, success, danger, warning)
- **Plugins**: None required (all custom components defined in CSS)

## Custom Component Classes

Pre-defined in `src/css/tailwind.css`:

### Buttons
- `.btn-primary`, `.btn-secondary`, `.btn-success`, `.btn-danger`, `.btn-warning`, `.btn-ghost`
- `.btn-sm`, `.btn-lg`

### Forms
- `.form-input`, `.form-select`, `.form-checkbox`, `.form-radio`
- `.form-label`, `.form-error`, `.form-hint`

### Cards
- `.card`, `.card-header`, `.card-body`, `.card-footer`

### Alerts
- `.alert-info`, `.alert-success`, `.alert-warning`, `.alert-danger`

### Badges
- `.badge-primary`, `.badge-success`, `.badge-danger`, `.badge-warning`, `.badge-secondary`

### Modals
- `.modal-overlay`, `.modal-content`, `.modal-header`, `.modal-body`, `.modal-footer`

### Tables
- `.table` (with responsive thead/tbody/td styling)

### Utilities
- `.fab` (floating action button)
- `.spinner` (loading spinner)
- `.link` (styled link)

## JavaScript Utilities

Exposed on `window` for Blazor interop:

### Theme Management
```javascript
LogMyDayTheme.get()      // Returns 'light' or 'dark'
LogMyDayTheme.set(theme) // Set theme ('light' or 'dark')
LogMyDayTheme.toggle()   // Toggle between themes
```

### Modal Management
```javascript
LogMyDayModal.show(modalId)
LogMyDayModal.hide(modalId)
```

### Date Picker Helpers
```javascript
LogMyDayDatePicker.isMobile() // Returns boolean
```

### Scroll Utilities
```javascript
LogMyDayScroll.toTop()
LogMyDayScroll.toElement(elementId)
```

## Production Build

The production build process:

1. Processes Tailwind directives
2. Purges unused CSS (only includes classes found in content files)
3. Minifies CSS with PostCSS
4. Minifies JavaScript with Terser (removes console.log statements)
5. Outputs to `dist/` directory

Expected output sizes:
- **CSS**: ~25-30KB (gzipped: ~4-5KB)
- **JS**: ~1-2KB (gzipped: <1KB)

## Troubleshooting

### Styles not updating?
1. Rebuild: `npm run build`
2. Check that your `.razor` files are in the `content` paths in `tailwind.config.js`
3. Clear browser cache

### Build errors in .NET project?
1. Ensure npm dependencies are installed: `cd ui && npm install`
2. Check that Node.js is in your PATH
3. Verify MSBuild targets are configured in `.csproj` files

### CSS file too large?
1. Check that `content` paths in `tailwind.config.js` are not too broad
2. Verify you're not importing entire Tailwind without `@layer` directives
3. Ensure production build is purging unused classes

### Dark mode not working?
1. Verify `dark` class is on `<html>` element
2. Check that FOUC prevention script is in `<head>` (before CSS loads)
3. Confirm localStorage key is `lmd-theme`

## Development Workflow

### Adding a New Icon
1. Find SVG on https://heroicons.com/
2. Add new case to `LogMyDay.UI/Components/Icons/Icon.razor`
3. Ensure SVG uses `stroke="currentColor"` or `fill="currentColor"`

### Adding a New Component Class
1. Open `ui/src/css/tailwind.css`
2. Add to `@layer components { ... }`
3. Rebuild: `npm run build`

### Modifying Theme Colors
1. Edit `ui/tailwind.config.js` in the `theme.extend.colors` section
2. Rebuild: `npm run build`
3. Update existing components to use new color names

## Dependencies

### Production
- `tailwindcss` - Utility-first CSS framework
- `postcss` - CSS transformation tool
- `autoprefixer` - Adds vendor prefixes

### Development
- `vite` - Build tool and dev server
- `terser` - JavaScript minifier

## License

This build system is part of the LogMyDay project.
