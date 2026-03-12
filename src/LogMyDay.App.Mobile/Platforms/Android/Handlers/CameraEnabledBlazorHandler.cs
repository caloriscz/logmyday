using Microsoft.AspNetCore.Components.WebView.Maui;

namespace LogMyDay.App.Mobile.Platforms.Android.Handlers;

/// <summary>
/// Custom BlazorWebView handler that configures the underlying Android WebView
/// to grant camera permission requests from JavaScript (getUserMedia).
/// </summary>
public class CameraEnabledBlazorHandler : BlazorWebViewHandler
{
    protected override void ConnectHandler(global::Android.Webkit.WebView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.Settings.MediaPlaybackRequiresUserGesture = false;

        var existingClient = platformView.WebChromeClient;
        platformView.SetWebChromeClient(new CameraPermissionChromeClient(existingClient));
    }
}
