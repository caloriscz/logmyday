using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Preferences;

namespace LogMyDay.App.Mobile.Services;

public interface IUserPreferencesService
{
    Task<UserPreferencesSnapshot> GetAsync(CancellationToken cancellationToken = default);
    void InvalidateCache();
    event EventHandler? PreferencesChanged;
}

public sealed class UserPreferencesService : IUserPreferencesService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IApiClientProvider _apiClientProvider;
    private readonly Diagnostics.IDiagnosticStore _diag;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private UserPreferencesSnapshot? _cached;
    private DateTime _cacheExpiresUtc = DateTime.MinValue;

    public event EventHandler? PreferencesChanged;

    public UserPreferencesService(IApiClientProvider apiClientProvider, Diagnostics.IDiagnosticStore diag)
    {
        _apiClientProvider = apiClientProvider;
        _diag = diag;
    }

    public async Task<UserPreferencesSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: return cached value if not expired
        if (_cached is not null && DateTime.UtcNow < _cacheExpiresUtc)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_cached is not null && DateTime.UtcNow < _cacheExpiresUtc)
            {
                return _cached;
            }

            var authApi = _apiClientProvider.Auth;
            var currentUser = await authApi.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);

            // Diagnostic store is admin-only; persist the decision so out-of-process receivers inherit it.
            _diag.SetEnabled(currentUser.IsAdmin);

            if (_cached is null || !EqualityComparer<CurrentUserDto>.Default.Equals(_cached.CurrentUser, currentUser))
            {
                var preferences = PreferencesFactory.From(currentUser.Culture, currentUser.TimeZone);
                var culture = CultureInfo.GetCultureInfo(preferences.Culture);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(preferences.TimeZoneId);

                _cached = new UserPreferencesSnapshot(currentUser, preferences, culture, timeZone);
                
                PreferencesChanged?.Invoke(this, EventArgs.Empty);
            }

            _cacheExpiresUtc = DateTime.UtcNow.Add(CacheTtl);
        }
        finally
        {
            _gate.Release();
        }

        return _cached!;
    }

    public void InvalidateCache()
    {
        _cached = null;
        _cacheExpiresUtc = DateTime.MinValue;
    }
}

public sealed record UserPreferencesSnapshot(
    CurrentUserDto CurrentUser,
    EffectivePreferences Preferences,
    CultureInfo Culture,
    TimeZoneInfo TimeZone);
