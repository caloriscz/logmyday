using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile.Platforms.Android.Handlers;

/// <summary>
/// Custom BlazorWebView handler that configures the underlying Android WebView
/// to grant camera permission requests from JavaScript (getUserMedia) and exposes the
/// native callback bridge (HARD RULE #1: no IJSRuntime).
/// </summary>
public class CameraEnabledBlazorHandler : BlazorWebViewHandler
{
    protected override void ConnectHandler(global::Android.Webkit.WebView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.Settings.MediaPlaybackRequiresUserGesture = false;

        var existingClient = platformView.WebChromeClient;
        platformView.SetWebChromeClient(new CameraPermissionChromeClient(existingClient));

        var bridge = IPlatformApplication.Current?.Services?.GetService<INativeCallbackBridge>();
        if (bridge is not null)
        {
            platformView.AddJavascriptInterface(new WebViewNativeBridge(bridge), "LmdNative");
        }
    }
}
