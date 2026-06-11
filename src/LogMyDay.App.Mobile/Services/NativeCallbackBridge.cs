namespace LogMyDay.App.Mobile.Services;

/// <summary>
/// Routes JS -> .NET callbacks coming from the native WebView JavascriptInterface to the
/// component that registered a token. Replaces IJSRuntime + DotNetObjectReference callbacks
/// (HARD RULE #1). Components register a token while a JS interaction is active and unregister
/// when it ends; the platform interface calls <see cref="Dispatch"/> with that token.
/// </summary>
public interface INativeCallbackBridge
{
    void Register(string token, Func<string?, string?, Task> callback);
    void Unregister(string token);

    /// <summary>Invoked by the platform JavascriptInterface when JS calls back.</summary>
    void Dispatch(string token, string? arg0, string? arg1);
}

public sealed class NativeCallbackBridge : INativeCallbackBridge
{
    private readonly Dictionary<string, Func<string?, string?, Task>> _callbacks = new();
    private readonly object _gate = new();

    public void Register(string token, Func<string?, string?, Task> callback)
    {
        lock (_gate)
        {
            _callbacks[token] = callback;
        }
    }

    public void Unregister(string token)
    {
        lock (_gate)
        {
            _callbacks.Remove(token);
        }
    }

    public void Dispatch(string token, string? arg0, string? arg1)
    {
        Func<string?, string?, Task>? callback;
        lock (_gate)
        {
            _callbacks.TryGetValue(token, out callback);
        }

        // Dispatch arrives on a native binder thread; the component callback marshals back to
        // the Blazor renderer via ComponentBase.InvokeAsync.
        _ = callback?.Invoke(arg0, arg1);
    }
}
