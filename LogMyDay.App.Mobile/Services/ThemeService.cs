using Microsoft.JSInterop;

namespace LogMyDay.App.Mobile.Services;

public enum AppTheme
{
    Light,
    Dark,
    System
}

public interface IThemeService
{
    Task<AppTheme> GetThemeAsync();
    Task SetThemeAsync(AppTheme theme);
    Task InitializeAsync();
}

public class ThemeService : IThemeService
{
    private const string ThemePreferenceKey = "app_theme";
    private readonly IJSRuntime _jsRuntime;

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public Task<AppTheme> GetThemeAsync()
    {
        var themeString = Preferences.Get(ThemePreferenceKey, "system");
        var theme = themeString.ToLowerInvariant() switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.System
        };
        return Task.FromResult(theme);
    }

    public async Task SetThemeAsync(AppTheme theme)
    {
        var themeString = theme switch
        {
            AppTheme.Light => "light",
            AppTheme.Dark => "dark",
            _ => "system"
        };

        Preferences.Set(ThemePreferenceKey, themeString);
        await ApplyThemeAsync(theme);
    }

    public async Task InitializeAsync()
    {
        var theme = await GetThemeAsync();
        await ApplyThemeAsync(theme);
    }

    private async Task ApplyThemeAsync(AppTheme theme)
    {
        try
        {
            var themeString = theme switch
            {
                AppTheme.Light => "light",
                AppTheme.Dark => "dark",
                _ => "system"
            };

            System.Diagnostics.Debug.WriteLine($"[ThemeService] Applying theme: {themeString}");
            
            // In MAUI, we need to use the WebView's EvaluateJavaScriptAsync instead of IJSRuntime
            // Get the MainPage's BlazorWebView using the modern Window API
            var mainPage = Application.Current?.Windows?[0]?.Page as MainPage;
            if (mainPage != null)
            {
                var script = $@"
                    if (typeof setTheme === 'function') {{
                        setTheme('{themeString}');
                        console.log('[ThemeService] Theme set via WebView: {themeString}');
                    }} else {{
                        console.error('[ThemeService] setTheme function not found');
                    }}
                ";
                
                await mainPage.RunJavaScriptAsync(script);
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Theme applied successfully via WebView: {themeString}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] MainPage not available, cannot apply theme");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] ERROR applying theme: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Stack trace: {ex.StackTrace}");
        }
    }
}
