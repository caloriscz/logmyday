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
        System.Diagnostics.Debug.WriteLine("[MainPage] Initialized - simple reload button approach");
    }

    /// <summary>
    /// Execute JavaScript in the BlazorWebView context (used by ThemeService and other services)
    /// </summary>
    public async Task<string?> RunJavaScriptAsync(string script)
    {
        try
        {
#if ANDROID
          if (blazorWebView?.Handler?.PlatformView is Android.Webkit.WebView webView)
          {
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
#endif  
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
