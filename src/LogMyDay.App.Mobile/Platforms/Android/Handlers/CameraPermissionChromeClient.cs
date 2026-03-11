using Android.OS;
using Android.Webkit;

namespace LogMyDay.App.Mobile.Platforms.Android.Handlers;

/// <summary>
/// Wraps the existing BlazorWebChromeClient to handle camera permission requests
/// from the WebView. When html5-qrcode calls getUserMedia, the WebView calls
/// OnPermissionRequest — without this, camera access is denied by default.
/// </summary>
internal class CameraPermissionChromeClient : WebChromeClient
{
    private readonly WebChromeClient? _inner;

    public CameraPermissionChromeClient(WebChromeClient? inner)
    {
        _inner = inner;
    }

    public override void OnPermissionRequest(PermissionRequest? request)
    {
        if (request?.GetResources() is { } resources)
        {
            var granted = resources
                .Where(r => r == PermissionRequest.ResourceVideoCapture)
                .ToArray();

            if (granted.Length > 0)
            {
                request.Grant(granted);

                return;
            }
        }

        if (_inner != null)
        {
            _inner.OnPermissionRequest(request);
        }
        else
        {
            base.OnPermissionRequest(request);
        }
    }

    // Delegate Blazor-critical overrides to inner client

    public override bool OnShowFileChooser(global::Android.Webkit.WebView? webView, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams)
    {
        if (_inner != null)
        {
            return _inner.OnShowFileChooser(webView, filePathCallback, fileChooserParams);
        }

        return base.OnShowFileChooser(webView, filePathCallback, fileChooserParams);
    }

    public override bool OnCreateWindow(global::Android.Webkit.WebView? view, bool isDialog, bool isUserGesture, Message? resultMsg)
    {
        if (_inner != null)
        {
            return _inner.OnCreateWindow(view, isDialog, isUserGesture, resultMsg);
        }

        return base.OnCreateWindow(view, isDialog, isUserGesture, resultMsg);
    }

    public override void OnCloseWindow(global::Android.Webkit.WebView? window)
    {
        if (_inner != null)
        {
            _inner.OnCloseWindow(window);
        }
        else
        {
            base.OnCloseWindow(window);
        }
    }

    public override void OnProgressChanged(global::Android.Webkit.WebView? view, int newProgress)
    {
        if (_inner != null)
        {
            _inner.OnProgressChanged(view, newProgress);
        }
        else
        {
            base.OnProgressChanged(view, newProgress);
        }
    }

    public override bool OnConsoleMessage(ConsoleMessage? consoleMessage)
    {
        if (_inner != null)
        {
            return _inner.OnConsoleMessage(consoleMessage);
        }

        return base.OnConsoleMessage(consoleMessage);
    }

    public override void OnGeolocationPermissionsShowPrompt(string? origin, GeolocationPermissions.ICallback? callback)
    {
        if (_inner != null)
        {
            _inner.OnGeolocationPermissionsShowPrompt(origin, callback);
        }
        else
        {
            base.OnGeolocationPermissionsShowPrompt(origin, callback);
        }
    }

    public override void OnGeolocationPermissionsHidePrompt()
    {
        if (_inner != null)
        {
            _inner.OnGeolocationPermissionsHidePrompt();
        }
        else
        {
            base.OnGeolocationPermissionsHidePrompt();
        }
    }
}
