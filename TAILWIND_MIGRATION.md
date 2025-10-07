# Bootstrap to Tailwind Migration Guide for LogMyDay

## Phase 1: Build Infrastructure ✅ COMPLETE

### Completed Items:

1. **UI Workspace Setup** ✅
   - Created `ui/` directory with Vite build configuration
   - Installed: vite, tailwindcss, postcss, autoprefixer, terser
   - Build output: `ui/dist/css/tailwind.css` and `ui/dist/js/app.js`

2. **Tailwind Configuration** ✅
   - File: `ui/tailwind.config.js`
   - Dark mode: `class` strategy
   - Content paths: Covers both LogMyDay.App and LogMyDay.App.Mobile
   - Custom theme: primary, success, danger, warning color palettes
   - Custom components: btn, form-input, card, alert, badge, modal, table classes

3. **Base CSS** ✅
   - File: `ui/src/css/tailwind.css`
   - Includes @tailwind directives for base, components, utilities
   - Custom component classes for common patterns
   - Mobile-optimized scrollbar styling
   - Animation utilities

4. **Theme Toggle System** ✅
   - File: `ui/src/js/app.js`
   - Functions: `LogMyDayTheme.get()`, `set()`, `toggle()`
   - localStorage persistence
   - System preference detection
   - FOUC prevention script in HTML head
   - Blazor component: `LogMyDay.UI/Components/ThemeToggle.razor`

5. **Icon Component** ✅
   - File: `LogMyDay.UI/Components/Icons/Icon.razor`
   - 30+ Heroicons inline SVGs
   - All icons use `stroke="currentColor"` for theme support
   - Usage: `<Icon Name="home" Class="w-5 h-5" />`

6. **MSBuild Integration** ✅
   - Web app: `LogMyDay.App/LogMyDay.App.csproj` - runs `npm run build` and copies assets
   - MAUI app: `LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj` - copies assets from `ui/dist/`
   - Build order: Vite build → copy CSS/JS to wwwroot

7. **HTML Head Updates** ✅
   - Web: `LogMyDay.App/Components/App.razor`
   - MAUI: `LogMyDay.App.Mobile/wwwroot/index.html`
   - Removed Bootstrap CSS/JS CDN links
   - Removed Flatpickr CDN links
   - Added `css/tailwind.css` reference
   - Added theme init script (prevents FOUC)
   - Added `js/app.js` reference

## Phase 2: Component Migration 🔄 IN PROGRESS

### Bootstrap → Tailwind Class Mappings:

#### Layout & Grid
```
container                 → container mx-auto px-4
row                       → flex flex-wrap -mx-2
col-*                     → flex-1 px-2 (or specific w-* classes)
d-flex                    → flex
justify-content-center    → justify-center
align-items-center        → items-center
```

#### Typography
```
h1, h2, h3               → text-3xl, text-2xl, text-xl font-bold
lead                      → text-lg
text-muted                → text-gray-600 dark:text-gray-400
fw-bold                   → font-bold
text-center               → text-center
```

#### Buttons
```
btn btn-primary           → btn-primary
btn btn-secondary         → btn-secondary
btn btn-success           → btn-success
btn btn-danger            → btn-danger
btn btn-sm                → btn-sm
btn-close                 → <Icon Name="x" />
```

#### Forms
```
form-label                → form-label
form-control              → form-input
form-select               → form-select
form-check-input          → form-checkbox / form-radio
was-validated             → [use Blazor validation classes]
invalid-feedback          → form-error
```

#### Cards
```
card                      → card
card-header               → card-header
card-body                 → card-body
card-footer               → card-footer
```

#### Alerts
```
alert alert-primary       → alert-info
alert alert-success       → alert-success
alert alert-warning       → alert-warning
alert alert-danger        → alert-danger
```

#### Badges
```
badge bg-primary          → badge-primary
badge bg-success          → badge-success
badge bg-danger           → badge-danger
```

#### Modals
```
modal                     → hidden (toggle with JS)
modal-dialog              → modal-overlay
modal-content             → modal-content max-w-lg
modal-header              → modal-header
modal-body                → modal-body
modal-footer              → modal-footer
data-bs-dismiss="modal"   → @onclick="CloseModal"
```

#### Navigation
```
navbar                    → (custom Tailwind nav)
navbar-brand              → text-xl font-bold
nav-link                  → px-3 py-2 hover:bg-gray-100
navbar-toggler            → lg:hidden (mobile menu button)
```

#### Utilities
```
mt-3, mb-3, ms-2, me-2    → mt-3, mb-3, ms-2, me-2 (mostly same)
p-3, px-4, py-2           → p-3, px-4, py-2 (mostly same)
text-primary              → text-primary-600 dark:text-primary-400
bg-light                  → bg-gray-100 dark:bg-gray-800
border                    → border border-gray-300 dark:border-gray-700
rounded                   → rounded-lg
shadow                    → shadow-sm
```

### Files Requiring Conversion:

#### Layouts (Priority: HIGH)
- [ ] `LogMyDay.App/Components/Layout/MainLayout.razor` (598 lines)
- [ ] `LogMyDay.App/Components/Layout/NavMenu.razor` (240 lines)
- [ ] `LogMyDay.App.Mobile/Components/Layout/MainLayout.razor`

#### Pages (Priority: HIGH)
- [ ] `LogMyDay.App/Components/Pages/Home.razor`
- [ ] `LogMyDay.App/Components/Pages/Tags.razor`
- [ ] `LogMyDay.App/Components/Pages/Activities.razor` (if exists)
- [ ] `LogMyDay.App/Components/Pages/Backup.razor`
- [ ] `LogMyDay.App/Components/Pages/Notifications.razor`
- [ ] `LogMyDay.App/Components/Pages/Login.razor`

#### Shared Components (Priority: MEDIUM)
- [ ] Modal components (AddActivityModal, etc.)
- [ ] Form components
- [ ] Card components
- [ ] Alert/notification components

#### App CSS (Priority: HIGH)
- [ ] `LogMyDay.App/wwwroot/app.css` - Remove Bootstrap overrides, keep custom styles
- [ ] Convert any Bootstrap-specific CSS to Tailwind utilities

### Date Picker Migration:

**Remove Flatpickr:**
1. Delete `js/flatpickr-integration.js`
2. Remove all `@ref` attributes pointing to Flatpickr inputs
3. Remove JS interop calls to Flatpickr

**Replace with Native Date Picker:**
```razor
<!-- OLD: Flatpickr -->
<input type="text" class="form-control flatpickr" @ref="dateInput" />

<!-- NEW: Native HTML5 -->
<input type="date" class="form-input" @bind="selectedDate" />
<input type="time" class="form-input" @bind="selectedTime" />
<input type="datetime-local" class="form-input" @bind="selectedDateTime" />
```

**Mobile Detection (Optional):**
```csharp
var isMobile = await JSRuntime.InvokeAsync<bool>("LogMyDayDatePicker.isMobile");
if (isMobile) {
    // Use native picker
} else {
    // Use native picker (or optional Litepicker if needed)
}
```

## Phase 3: Testing Checklist

### Web App Testing
- [ ] Build succeeds: `dotnet build LogMyDay.App/LogMyDay.App.csproj`
- [ ] UI assets generated in `wwwroot/css/` and `wwwroot/js/`
- [ ] Home page renders without Bootstrap
- [ ] Navigation menu works (desktop & mobile)
- [ ] Theme toggle button visible and functional
- [ ] Dark mode persists across page reloads
- [ ] Forms validate correctly
- [ ] Modals open/close properly
- [ ] Date pickers work (native inputs)
- [ ] All icons display correctly
- [ ] No console errors
- [ ] No references to Bootstrap classes in DOM

### MAUI Mobile App Testing
- [ ] Build succeeds: `dotnet build LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj`
- [ ] Tailwind CSS loads in WebView
- [ ] Theme toggle works
- [ ] Native date picker appears on date inputs (Android keyboard)
- [ ] Touch targets are adequately sized (min 44x44px)
- [ ] FAB button accessible
- [ ] Bottom navigation works
- [ ] No layout overflow issues
- [ ] No references to Bootstrap

### Production Build
- [ ] Run: `cd ui && npm run build`
- [ ] CSS file size reasonable (<50KB after gzip)
- [ ] No unused Tailwind classes (purged correctly)
- [ ] JS minified and console logs removed
- [ ] No external CDN dependencies

## Phase 4: Cleanup

### Files to Delete:
- [ ] `wwwroot/lib/bootstrap/` (if vendored)
- [ ] `wwwroot/js/flatpickr-integration.js`
- [ ] Any Bootstrap overrides in `app.css`

### Dependencies to Remove:
Check `package.json` or NuGet packages for:
- Bootstrap-related packages
- Flatpickr packages

## Quick Start Commands

```powershell
# Install UI dependencies
cd ui
npm install

# Build Tailwind CSS
npm run build

# Watch mode (during development)
npm run dev

# Build .NET app (triggers Vite build automatically)
cd ..
dotnet build LogMyDay.App/LogMyDay.App.csproj

# Run web app
dotnet run --project LogMyDay.App/LogMyDay.App.csproj

# Build MAUI app
dotnet build LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj
```

## Common Migration Patterns

### Pattern 1: Simple Class Replacement
```razor
<!-- Before -->
<div class="card">
    <div class="card-header">
        <h5 class="card-title">Title</h5>
    </div>
    <div class="card-body">
        <p class="card-text">Content</p>
    </div>
</div>

<!-- After -->
<div class="card">
    <div class="card-header">
        <h5 class="text-lg font-semibold">Title</h5>
    </div>
    <div class="card-body">
        <p>Content</p>
    </div>
</div>
```

### Pattern 2: Modal Conversion
```razor
<!-- Before: Bootstrap Modal -->
<div class="modal fade" id="myModal" tabindex="-1">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Title</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">Content</div>
            <div class="modal-footer">
                <button class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                <button class="btn btn-primary">Save</button>
            </div>
        </div>
    </div>
</div>

<!-- After: Tailwind Modal -->
@if (showModal)
{
    <div class="modal-overlay" @onclick="CloseModal">
        <div class="modal-content max-w-lg" @onclick:stopPropagation="true">
            <div class="modal-header">
                <h5 class="text-lg font-semibold">Title</h5>
                <button @onclick="CloseModal" class="text-gray-400 hover:text-gray-600">
                    <Icon Name="x" Class="w-5 h-5" />
                </button>
            </div>
            <div class="modal-body">Content</div>
            <div class="modal-footer">
                <button @onclick="CloseModal" class="btn-secondary">Close</button>
                <button @onclick="Save" class="btn-primary">Save</button>
            </div>
        </div>
    </div>
}

@code {
    private bool showModal = false;
    private void CloseModal() => showModal = false;
    private void OpenModal() => showModal = true;
}
```

### Pattern 3: Form with Validation
```razor
<!-- Before -->
<EditForm Model="model" OnValidSubmit="Submit">
    <DataAnnotationsValidator />
    <div class="mb-3">
        <label class="form-label">Name</label>
        <InputText @bind-Value="model.Name" class="form-control" />
        <ValidationMessage For="@(() => model.Name)" class="invalid-feedback d-block" />
    </div>
    <button type="submit" class="btn btn-primary">Submit</button>
</EditForm>

<!-- After -->
<EditForm Model="model" OnValidSubmit="Submit">
    <DataAnnotationsValidator />
    <div class="mb-4">
        <label class="form-label">Name</label>
        <InputText @bind-Value="model.Name" class="form-input" />
        <ValidationMessage For="@(() => model.Name)" class="form-error" />
    </div>
    <button type="submit" class="btn-primary">Submit</button>
</EditForm>
```

### Pattern 4: Responsive Grid
```razor
<!-- Before -->
<div class="row">
    <div class="col-12 col-md-6 col-lg-4">Card 1</div>
    <div class="col-12 col-md-6 col-lg-4">Card 2</div>
    <div class="col-12 col-md-6 col-lg-4">Card 3</div>
</div>

<!-- After -->
<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
    <div>Card 1</div>
    <div>Card 2</div>
    <div>Card 3</div>
</div>
```

## Next Steps

1. **Convert MainLayout.razor** - This is the entry point and will set the foundation
2. **Convert NavMenu.razor** - Critical for navigation
3. **Convert one page at a time** - Start with simplest (Login, Home)
4. **Test after each conversion** - Don't convert everything at once
5. **Update CSS** - Remove Bootstrap-specific styles from app.css
6. **Final testing** - Full regression test on web and mobile

## Need Help?

### Icon doesn't exist?
Add it to `Icon.razor` by finding the SVG from https://heroicons.com/

### Need a new Tailwind component class?
Add it to `ui/src/css/tailwind.css` in the `@layer components` section

### Tailwind not updating?
1. Make sure content paths in `tailwind.config.js` include your files
2. Rebuild: `cd ui && npm run build`
3. Clear browser cache

### Build errors?
1. Check MSBuild targets are running: Look for "npm run build" in build output
2. Check paths in .csproj files
3. Verify node_modules exists in ui/
