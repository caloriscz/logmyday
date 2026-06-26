using System.Text.Json;

namespace LogMyDay.App.Mobile.Services;

/// <summary>
/// Runs JavaScript in the BlazorWebView through the native WebView API.
/// HARD RULE #1: IJSRuntime must never be used in MAUI Blazor. This is one-way
/// (.NET -> JS); use native MAUI dialogs (e.g. ConfirmAsync) when a result is required.
/// </summary>
public interface IWebViewInterop
{
    /// <summary>Execute a raw script in the WebView.</summary>
    Task RunScriptAsync(string script);

    /// <summary>Call a global JS function, JSON-encoding each argument into a safe JS literal.</summary>
    Task InvokeVoidAsync(string function, params object?[] args);

    /// <summary>Show a native confirmation dialog and return the user's choice.</summary>
    Task<bool> ConfirmAsync(string title, string message, string accept = "OK", string cancel = "Cancel");
}

public sealed class WebViewInterop : IWebViewInterop
{
    public async Task RunScriptAsync(string script)
    {
        if (CurrentPage is MainPage page)
        {
            await page.RunJavaScriptAsync(script);
        }
    }

    public Task InvokeVoidAsync(string function, params object?[] args)
    {
        var encoded = args is { Length: > 0 }
            ? string.Join(", ", args.Select(a => JsonSerializer.Serialize(a)))
            : string.Empty;

        return RunScriptAsync($"{function}({encoded});");
    }

    public async Task<bool> ConfirmAsync(string title, string message, string accept = "OK", string cancel = "Cancel")
    {
        if (CurrentPage is Page page)
        {
            return await page.DisplayAlert(title, message, accept, cancel);
        }

        return false;
    }

    private static Page? CurrentPage =>
        Application.Current?.Windows is { Count: > 0 } windows ? windows[0].Page : null;
}
