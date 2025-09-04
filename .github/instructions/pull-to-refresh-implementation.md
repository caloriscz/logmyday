# Pull-to-Refresh Implementation for LogMyDay Mobile

This document describes the pull-to-refresh functionality implemented in the LogMyDay mobile application.

## Overview

The pull-to-refresh feature allows users to refresh data-driven pages by pulling down from the top of the page content, similar to native mobile apps. This provides an intuitive way to reload data without needing to use separate refresh buttons.

## Implementation

### RefreshView Component

A reusable `RefreshView` component was created that can wrap any content to add pull-to-refresh functionality.

**Location:** `LogMyDay.App.Mobile/Components/Shared/RefreshView.razor`

**Key Features:**
- Platform-native touch event handling via JavaScript interop
- Smooth pull animations with resistance effect
- Visual feedback with spinner and text indicators
- Configurable pull threshold (80px by default)
- Maximum pull distance to prevent over-scrolling (120px)
- Automatic state management (pulling, refreshing, idle)

### JavaScript Integration

**File:** `LogMyDay.App.Mobile/wwwroot/js/refresh-view.js`

Handles:
- Touch event detection and processing
- DOM manipulation for smooth animations
- Memory management and cleanup
- Cross-device compatibility

### Applied to Data-Driven Pages

Pull-to-refresh has been implemented on the following pages:

#### 1. Activities Page (`/activities`)
- **Refresh Action:** Reloads activities data using current filters and date selection
- **Preserves:** User's selected display type, date, and filter settings

#### 2. Tags Page (`/tags`)
- **Refresh Action:** Reloads the complete list of available tags
- **Updates:** Tag information including types, granularity, and settings

#### 3. Quick Activities Page (`/quick`)
- **Refresh Action:** Refreshes quick activity buttons and available tags
- **Maintains:** Button states and cooldown timers

#### 4. Notifications Page (`/notifications`)
- **Refresh Action:** Updates unfilled required activities for the selected date
- **Preserves:** Selected date and modal state

### Pages NOT Implementing Pull-to-Refresh

- **Login Page:** Authentication form, not data-driven
- **Home Pages:** Static welcome/landing content
- **Form/Modal Pages:** Create/edit interfaces, not list views

## Usage

### For Developers

To add pull-to-refresh to a new data-driven page:

1. Wrap your content with `RefreshView`:
```razor
<RefreshView OnRefresh="RefreshData" IsEnabled="@(!isRefreshing)">
    <!-- Your page content -->
</RefreshView>
```

2. Add refresh state management:
```csharp
private bool isRefreshing = false;

private async Task RefreshData()
{
    isRefreshing = true;
    
    try
    {
        await LoadYourData();
    }
    finally
    {
        isRefreshing = false;
    }
}
```

### For Users

1. **Pull Down:** On any data page, pull down from the top
2. **Visual Feedback:** See spinner and "Pull to refresh" text
3. **Release:** When pulled far enough, release to trigger refresh
4. **Wait:** Content refreshes automatically and returns to normal

## Technical Details

### Touch Event Handling

- **touchstart**: Captures initial touch position
- **touchmove**: Calculates pull distance with resistance
- **touchend**: Triggers refresh if threshold met
- **Smooth Animations**: CSS transitions for visual feedback

### Performance Considerations

- Events only processed when at scroll top
- Resistance applied to prevent excessive pulling
- Memory cleanup on component disposal
- Debounced refresh to prevent multiple calls

### Cross-Platform Compatibility

- Tested on Android devices
- Uses standard web touch events
- Fallback handling for older browsers
- Responsive design for different screen sizes

## Styling

The pull-to-refresh indicator uses Bootstrap-compatible styling:
- Light gray background matching app theme
- Primary color spinner for consistency
- Smooth transitions with reduced motion support
- Mobile-optimized touch targets

## Future Enhancements

Potential improvements for future versions:
- Haptic feedback on supported devices
- Custom refresh indicators per page type
- Pull-to-refresh statistics/analytics
- Improved accessibility features
- Gesture customization options
