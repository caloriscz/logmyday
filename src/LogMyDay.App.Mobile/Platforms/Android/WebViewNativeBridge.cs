using Android.Webkit;
using Java.Interop;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile.Platforms.Android;

/// <summary>
/// Android JavascriptInterface exposed to the WebView as <c>window.LmdNative</c>. It is the native
/// replacement for IJSRuntime DotNetObjectReference callbacks (HARD RULE #1): JS calls these methods
/// and they forward to the managed <see cref="INativeCallbackBridge"/>. Only the local, app-bundled
/// WebView content runs here, so exposing it to JS is safe.
/// </summary>
public sealed class WebViewNativeBridge : Java.Lang.Object
{
    private readonly INativeCallbackBridge _bridge;

    public WebViewNativeBridge(INativeCallbackBridge bridge)
    {
        _bridge = bridge;
    }

    [JavascriptInterface]
    [Export("onScan")]
    public void OnScan(string token, string decodedText, string format)
    {
        _bridge.Dispatch(token, decodedText, format);
    }

    [JavascriptInterface]
    [Export("onOutsideClick")]
    public void OnOutsideClick(string token)
    {
        _bridge.Dispatch(token, null, null);
    }
}
