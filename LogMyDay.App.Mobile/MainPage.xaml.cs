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

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            // Notify the current Blazor page to refresh its data
            RefreshService.RequestRefresh();
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the app
            System.Diagnostics.Debug.WriteLine($"Error during refresh: {ex.Message}");
        }
        finally
        {
            // Stop the refresh animation after a short delay to allow Blazor to respond
            Task.Delay(1000).ContinueWith(_ => 
            {
                Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() => 
                {
                    refreshView.IsRefreshing = false;
                });
            });
        }
    }
}
