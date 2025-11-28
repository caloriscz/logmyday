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
    private readonly IApiClientProvider _apiClientProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private UserPreferencesSnapshot? _cached;

    public event EventHandler? PreferencesChanged;

    public UserPreferencesService(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
    }

    public async Task<UserPreferencesSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var authApi = _apiClientProvider.Auth;
            var currentUser = await authApi.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);

            if (_cached is null || !EqualityComparer<CurrentUserDto>.Default.Equals(_cached.CurrentUser, currentUser))
            {
                var preferences = PreferencesFactory.From(currentUser.Culture, currentUser.TimeZone);
                var culture = CultureInfo.GetCultureInfo(preferences.Culture);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(preferences.TimeZoneId);

                _cached = new UserPreferencesSnapshot(currentUser, preferences, culture, timeZone);
                
                PreferencesChanged?.Invoke(this, EventArgs.Empty);
            }
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
    }
}

public sealed record UserPreferencesSnapshot(
    CurrentUserDto CurrentUser,
    EffectivePreferences Preferences,
    CultureInfo Culture,
    TimeZoneInfo TimeZone);
