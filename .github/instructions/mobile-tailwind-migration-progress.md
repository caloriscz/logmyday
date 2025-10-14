# Mobile App Tailwind Migration Progress

## Overview
This document tracks the migration of LogMyDay.App.Mobile from Bootstrap to Tailwind CSS, following the patterns established in LogMyDay.App.

## Completed Tasks

### 1. ✅ Mobile-Specific CSS Classes Added
**File:** `LogMyDay.App.Mobile/wwwroot/app.css`

Added Tailwind-based mobile layout classes:
- `.mobile-container` - Full height flex container with proper dark theme support
- `.mobile-content` - Scrollable content area with bottom navigation spacing
- `.mobile-nav` - Fixed bottom navigation bar with safe area insets
- `.mobile-nav-item` - Navigation button with proper touch targets (48px minimum)
- `.fab` - Floating Action Button positioned above mobile navigation

**Key Features:**
- Dark/light theme support using `[data-bs-theme="dark"]` selectors
- Safe area insets for modern devices with notches
- Minimum 48px touch targets for accessibility
- Proper z-index layering (mobile-nav and FAB both at z-50)
- Smooth transitions and hover states

### 2. 🔄 Activities.razor Partial Conversion
**File:** `LogMyDay.App.Mobile/Components/Pages/Activities.razor`

**Completed:**
- ✅ Removed `<MobileTopbar />` component
- ✅ Added page header with Tailwind classes
- ✅ Converted filter and sort controls to Tailwind
- ✅ Updated error/loading messages to use Tailwind
- ✅ Converted daily view date navigation to Tailwind
- ✅ Updated activity cards for both mobile and desktop views
- ✅ Converted table view to use Tailwind classes

**Remaining in Activities.razor:**
- ⏳ Filter Modal - needs conversion to Tailwind modal classes
- ⏳ AddActivityModal component integration check

## Pending Tasks

### 3. Remove MobileTopbar from Other Pages
**Files to update:**
- `LogMyDay.App.Mobile/Components/Pages/Tags.razor`
- `LogMyDay.App.Mobile/Components/Pages/Settings.razor`
- `LogMyDay.App.Mobile/Components/Pages/Quick.razor`
- `LogMyDay.App.Mobile/Components/Pages/PasswordSettings.razor`
- `LogMyDay.App.Mobile/Components/Pages/Exercise.razor`
- `LogMyDay.App.Mobile/Components/Pages/Breathing.razor`
- `LogMyDay.App.Mobile/Components/Pages/AccountSettings.razor`
- `LogMyDay.App.Mobile/Components/Pages/Notifications.razor`

**Pattern to follow:**
```razor
<!-- Remove this: -->
<MobileTopbar Title="Page Name" />

<!-- Replace with: -->
<div class="px-4 py-4">
    <!-- Page Header -->
    <div class="mb-6">
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Page Name</h1>
    </div>
    
    <!-- Rest of page content -->
</div>
```

### 4. Convert Home.razor to Tailwind
**File:** `LogMyDay.App.Mobile/Components/Pages/Home.razor`

**Bootstrap to Tailwind Mappings:**
```
container-fluid → px-4 py-4
card → card (use custom .card class from app.css)
card-body → (remove - use padding in .card)
alert alert-success → alert-success
alert alert-info → alert-info
d-grid gap-2 → flex flex-col gap-2
btn btn-primary → btn-primary
btn-outline-primary → btn-secondary
```

### 5. Update MainLayout.razor
**File:** `LogMyDay.App.Mobile/Components/Layout/MainLayout.razor`

The navigation structure is already using the correct classes (`.mobile-nav`, `.mobile-nav-item`), but verify:
- ✅ `.mobile-container` wrapper is present
- ✅ `.mobile-content` contains the `@Body`
- ✅ `.mobile-nav` bottom navigation is correctly positioned
- ⏳ Icon classes may need review (using Bootstrap Icons currently)

### 6. Convert Shared Components
**Priority Order:**
1. `SearchableSelect.razor` - Update form control styles
2. `AddActivityModal.razor` - Convert modal to Tailwind
3. `CultureAwareDatePicker.razor` - Update input styles
4. `RefreshView.razor` - Verify Tailwind compatibility

**Key Patterns:**
- Form controls: Use `.form-control`, `.form-select`, `.form-label` custom classes
- Modals: Use Bootstrap modals temporarily (they still work), or convert to Tailwind modal pattern
- Buttons: Use `.btn-primary`, `.btn-secondary`, `.btn-danger` custom classes

### 7. Form Control Improvements
Apply the improvements from `form-controls-dark-theme-improvements.md`:
- Consistent focus states with sky-500 borders
- Proper readonly/disabled styling
- Improved placeholder contrast
- Softer dark theme text colors (slate-200 instead of slate-100)

**Files to update:**
- All pages with form inputs
- All shared components with forms

### 8. Test & Verify
**Testing Checklist:**
- [ ] Mobile navigation works correctly
- [ ] FAB button positioned correctly (bottom-right, above mobile nav)
- [ ] Dark/light theme switching works
- [ ] All touch targets are minimum 48px
- [ ] Safe area insets work on devices with notches
- [ ] Pull-to-refresh still functions
- [ ] Modals display correctly
- [ ] Forms are fully functional

## Tailwind Class Reference (Mobile-Specific)

### Layout
```css
/* Flex containers */
flex flex-col           → display: flex; flex-direction: column;
flex items-center       → display: flex; align-items: center;
flex justify-between    → display: flex; justify-content: space-between;

/* Spacing */
px-4 py-4              → padding: 1rem;
mb-4, mb-6             → margin-bottom: 1rem, 1.5rem;
gap-2, gap-3           → gap: 0.5rem, 0.75rem;

/* Sizing */
w-full                 → width: 100%;
h-screen               → height: 100vh;
min-h-48               → min-height: 3rem (48px);
```

### Typography
```css
text-2xl font-bold     → Large page headings
text-lg font-semibold  → Section headings
text-sm                → Body text, labels
text-xs                → Small helper text
```

### Colors (Dark Theme Support)
```css
text-gray-900 dark:text-white        → Main text
text-gray-600 dark:text-gray-400     → Muted text
bg-white dark:bg-gray-800            → Card backgrounds
border-gray-200 dark:border-gray-700 → Borders
```

### Responsive Classes
```css
sm:flex-row            → Applies flex-row on screens ≥640px
md:hidden              → Hides on screens ≥768px
lg:grid-cols-3         → 3-column grid on screens ≥1024px
```

## Custom CSS Classes (Keep Using)
These are defined in `app.css` and work with Tailwind:

```css
.card                  → Card container with padding and shadow
.btn-primary           → Primary action button
.btn-secondary         → Secondary action button
.btn-danger            → Destructive action button
.btn-sm                → Small button variant
.alert-success         → Success message
.alert-danger          → Error message
.alert-info            → Info message
.form-control          → Text input
.form-select           → Select dropdown
.form-label            → Form field label
.table                 → Table styling
```

## Migration Tips

### 1. Remove Bootstrap Grid
```razor
<!-- Before (Bootstrap) -->
<div class="container-fluid">
    <div class="row">
        <div class="col-md-6">Content</div>
    </div>
</div>

<!-- After (Tailwind) -->
<div class="px-4">
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>Content</div>
    </div>
</div>
```

### 2. Remove Bootstrap Flexbox
```razor
<!-- Before -->
<div class="d-flex justify-content-between align-items-center">

<!-- After -->
<div class="flex justify-between items-center">
```

### 3. Update Spacing Classes
```razor
<!-- Before -->
<div class="mb-3 mt-2">
<div class="px-2 py-3">

<!-- After -->
<div class="mb-3 mt-2">  <!-- Same! -->
<div class="px-2 py-3">  <!-- Same! -->
```

### 4. Maintain Mobile-First Approach
Always start with mobile styles, then add breakpoint modifiers:
```razor
<div class="text-center sm:text-left">     <!-- Center on mobile, left on desktop -->
<div class="flex-col md:flex-row">         <!-- Stack on mobile, row on desktop -->
<button class="w-full md:w-auto">          <!-- Full width on mobile, auto on desktop -->
```

## Notes
- The `.mobile-nav` stays at bottom (z-50)
- The `.fab` button positions above it (bottom: 5rem)
- Content padding-bottom accounts for mobile nav height
- Safe area insets handle device notches automatically
- All Bootstrap Icons (bi bi-*) remain unchanged
- Bootstrap modals can coexist with Tailwind during transition

## Next Steps
1. Complete Activities.razor filter modal conversion
2. Remove MobileTopbar from all pages
3. Convert Home.razor fully
4. Update remaining pages one by one
5. Convert shared components
6. Apply form control improvements
7. Test thoroughly on actual mobile device

## Related Documentation
- [Form Controls Dark Theme Improvements](./form-controls-dark-theme-improvements.md)
- [Tailwind Migration Guide](../TAILWIND_MIGRATION.md)
- [Tailwind Quick Reference](../TAILWIND_QUICK_REFERENCE.md)
- [Main Instructions](./instructions.md.instructions.md)
