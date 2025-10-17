# MAUI JavaScript Execution - Quick Reference

## ❌ WRONG - Don't Use IJSRuntime

```csharp
// This FAILS in MAUI with: "Cannot invoke JavaScript outside of a WebView context"
await _jsRuntime.InvokeVoidAsync("setTheme", "dark");
```

## ✅ CORRECT - Use Native WebView API

### Step 1: Add Helper to MainPage.xaml.cs

```csharp
#if ANDROID
using Android.Webkit;
#endif

public partial class MainPage : ContentPage
{
    public async Task<string?> RunJavaScriptAsync(string script)
    {
        #if ANDROID
        if (blazorWebView?.Handler?.PlatformView is Android.Webkit.WebView webView)
        {
            var tcs = new TaskCompletionSource<string?>();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                webView.EvaluateJavascript(script, new JavaScriptCallback(result =>
                {
                    tcs.SetResult(result);
                }));
            });
            return await tcs.Task;
        }
        #endif
        return null;
    }

    #if ANDROID
    private class JavaScriptCallback : Java.Lang.Object, IValueCallback
    {
        private readonly Action<string?> _callback;
        public JavaScriptCallback(Action<string?> callback) => _callback = callback;
        public void OnReceiveValue(Java.Lang.Object? value) => _callback?.Invoke(value?.ToString());
    }
    #endif
}
```

### Step 2: Use in Services

```csharp
public class ThemeService
{
    public async Task SetThemeAsync(string theme)
    {
        // Get MainPage using modern MAUI Window API
        var mainPage = Application.Current?.Windows?[0]?.Page as MainPage;
        
        if (mainPage != null)
        {
            var script = $"setTheme('{theme}')";
            await mainPage.RunJavaScriptAsync(script);
        }
    }
}
```

## Why This Is Necessary

| Blazor Web | MAUI Blazor |
|------------|-------------|
| Runs in browser | Runs natively |
| `IJSRuntime` works | `IJSRuntime` **fails** |
| Standard JS interop | Need platform-specific WebView API |

## Common Pitfalls

1. **Don't inject `IJSRuntime`** in MAUI services - it won't work
2. **Must call on UI thread** - use `MainThread.InvokeOnMainThreadAsync()`
3. **Platform-specific** - wrap in `#if ANDROID` / `#if IOS` conditionals
4. **Use modern Window API** - `Application.Current.Windows[0].Page` (not deprecated `MainPage`)

## Testing

After implementing, check Visual Studio Output window for debug messages:
```
[ThemeService] Applying theme: dark
[MainPage] WebView found, executing script...
[ThemeService] Theme applied successfully via WebView: dark
```

## Related Files

- Implementation: `LogMyDay.App.Mobile/MainPage.xaml.cs`
- Usage: `LogMyDay.App.Mobile/Services/ThemeService.cs`
- Full explanation: `.github/instructions/theme-system-debug-summary.md`
