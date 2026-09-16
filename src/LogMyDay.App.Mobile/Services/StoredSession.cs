namespace LogMyDay.App.Mobile.Services;

/// <summary>
/// Restores the session persisted at login into an <see cref="IApiContext"/>.
///
/// The UI does this itself when it starts (MainLayout), but background entry points do not have a
/// UI: Android recreates the process to deliver a reminder alarm, nothing renders MainLayout, and
/// <see cref="IApiContext"/> is a fresh unconfigured singleton. Every API call then throws
/// "API server not configured", so the alarm's cross-surface check fails open and fires reminders
/// that were already completed on the web.
///
/// Reads only what login already stored, and only into the in-memory context — the password stays
/// in platform SecureStorage and is never copied elsewhere.
/// </summary>
public static class StoredSession
{
    public const string ServerUrlKey = "ServerUrl";
    public const string UsernameKey = "Username";
    public const string PasswordKey = "Password";

    /// <summary>
    /// Configures <paramref name="ctx"/> from stored credentials. Returns true when the context is
    /// usable afterwards — including when it was already configured. Never throws.
    /// </summary>
    public static async Task<bool> TryRestore(IApiContext ctx)
    {
        if (ctx.IsConfigured)
        {
            return true;
        }

        var serverUrl = Preferences.Get(ServerUrlKey, string.Empty);
        var username = Preferences.Get(UsernameKey, string.Empty);

        if (string.IsNullOrWhiteSpace(serverUrl)
            || string.IsNullOrWhiteSpace(username)
            || !Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri))
        {
            return false;
        }

        string? password;

        // SecureStorage is keystore-backed and throws on devices where it is unavailable or the
        // key has been invalidated. A background caller has no way to recover, so treat any
        // failure as "no stored session" and let the caller fall back.
        try
        {
            password = await SecureStorage.Default.GetAsync(PasswordKey);
        }
        catch (Exception)
        {
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        ctx.Configure(serverUri, username, password);

        return true;
    }
}
