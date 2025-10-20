using LogMyDay.App.Mobile.Services;

#if ANDROID
using Android.Webkit;
#endif

namespace LogMyDay.App.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        refreshView.Refreshing += OnRefreshing;
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[RefreshView] OnRefreshing triggered");
            
#if ANDROID
            // FIRST: Check native WebView scroll position (fast, synchronous)
            if (blazorWebView?.Handler?.PlatformView is Android.Webkit.WebView webView)
            {
                var nativeScrollY = webView.ScrollY;
                System.Diagnostics.Debug.WriteLine($"[RefreshView] Native WebView ScrollY: {nativeScrollY}");
                
                // If native scroll is not at top, cancel immediately
                if (nativeScrollY > 5) // Small tolerance for floating point precision
                {
                    System.Diagnostics.Debug.WriteLine("[RefreshView] Native scroll not at top, cancelling");
                    refreshView.IsRefreshing = false;
                    return;
                }
            }
#endif
            
            // SECOND: Double-check with JavaScript scroll position (accurate but slower)
            var isAtTop = await CheckIfAtTopAsync();
            
            if (!isAtTop)
            {
                System.Diagnostics.Debug.WriteLine("[RefreshView] JavaScript confirms not at top, cancelling refresh");
                refreshView.IsRefreshing = false;
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("[RefreshView] At top (both checks passed), executing refresh");
            
            // Notify Blazor pages to refresh
            RefreshService.RequestRefresh();
            
            // Wait for refresh to complete
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefreshView] Error: {ex.Message}");
        }
        finally
        {
            refreshView.IsRefreshing = false;
        }
    }

    private async Task<bool> CheckIfAtTopAsync()
    {
        try
        {
            // Execute JavaScript synchronously and get result
            var result = await RunJavaScriptAsync(@"
                (function() {
                    try {
                        if (typeof window.getRefreshViewScrollTop === 'function') {
                            var targetScrollTop = window.getRefreshViewScrollTop();
                            return (targetScrollTop || 0).toString();
                        }

                        var fallback = document.querySelector('.mobile-content') ||
                                        document.querySelector('[data-refresh-scrollable]') ||
                                        document.scrollingElement ||
                                        document.documentElement ||
                                        document.body;

                        var scrollTop = 0;

                        if (fallback) {
                            if (fallback === document.body || fallback === document.documentElement) {
                                scrollTop = window.pageYOffset || fallback.scrollTop || 0;
                            } else {
                                scrollTop = fallback.scrollTop || 0;
                            }
                        }

                        console.log('Scroll position check (fallback):', scrollTop);
                        return (scrollTop || 0).toString();
                    } catch (err) {
                        console.error('RefreshView: Error during scroll position check', err);
                        return '1';
                    }
                })()
            ");
            
            System.Diagnostics.Debug.WriteLine($"[RefreshView] JavaScript returned: '{result}'");
            
            if (string.IsNullOrWhiteSpace(result))
            {
                System.Diagnostics.Debug.WriteLine("[RefreshView] Empty result, defaulting to NOT at top");
                return false;
            }
            
            if (int.TryParse(result.Trim(), out int scrollTop))
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshView] Parsed scroll position: {scrollTop}");
                bool isAtTop = scrollTop == 0;
                System.Diagnostics.Debug.WriteLine($"[RefreshView] Is at top: {isAtTop}");
                return isAtTop;
            }
            
            System.Diagnostics.Debug.WriteLine($"[RefreshView] Could not parse scroll position: '{result}'");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefreshView] Error checking scroll position: {ex.Message}");
            // On error, assume NOT at top to prevent unwanted refreshes
            return false;
        }
    }

    /// <summary>
    /// Execute JavaScript in the BlazorWebView context
    /// </summary>
    public async Task<string?> RunJavaScriptAsync(string script)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Attempting to execute JavaScript...");
            
#if ANDROID
            // Access the native Android WebView through the handler
            if (blazorWebView?.Handler?.PlatformView is Android.Webkit.WebView webView)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] WebView found, executing script...");
                
                // Execute JavaScript on the UI thread
                var tcs = new TaskCompletionSource<string?>();
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        webView.EvaluateJavascript(script, new JavaScriptCallback(result =>
                        {
                            tcs.SetResult(result);
                        }));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainPage] Error in EvaluateJavascript: {ex.Message}");
                        tcs.SetResult(null);
                    }
                });
                
                return await tcs.Task;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] Android WebView not found in handler");
            }
#endif
            
            System.Diagnostics.Debug.WriteLine("[MainPage] Platform not supported or WebView not available");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Error executing JavaScript: {ex.Message}");
            return null;
        }
    }

#if ANDROID
    private class JavaScriptCallback : Java.Lang.Object, IValueCallback
    {
        private readonly Action<string?> _callback;

        public JavaScriptCallback(Action<string?> callback)
        {
            _callback = callback;
        }

        public void OnReceiveValue(Java.Lang.Object? value)
        {
            _callback?.Invoke(value?.ToString());
        }
    }
#endif
}
