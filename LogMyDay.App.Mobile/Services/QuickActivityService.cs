using LogMyDay.App.Mobile.Models;
using System.Text.Json;

namespace LogMyDay.App.Mobile.Services;

public class QuickActivityService
{
    private const string QUICK_BUTTONS_KEY = "quick_activity_buttons";
    private readonly List<QuickActivityButton> _quickButtons = new();

    public event EventHandler<List<QuickActivityButton>>? QuickButtonsChanged;

    public async Task<List<QuickActivityButton>> GetQuickButtonsAsync()
    {
        if (_quickButtons.Count == 0)
        {
            await LoadQuickButtonsAsync();
        }
        
        return _quickButtons.ToList();
    }

    public async Task AddQuickButtonAsync(QuickActivityButton button)
    {
        System.Diagnostics.Debug.WriteLine($"🟢 SERVICE: Adding button '{button.Name}' for tag {button.TagName}");
        
        // Generate new ID
        button.Id = _quickButtons.Count > 0 ? _quickButtons.Max(b => b.Id) + 1 : 1;
        button.CreatedAt = DateTime.Now;
        
        _quickButtons.Add(button);
        
        System.Diagnostics.Debug.WriteLine($"🟢 SERVICE: Button added to list, total buttons: {_quickButtons.Count}");
        
        await SaveQuickButtonsAsync();
        
        System.Diagnostics.Debug.WriteLine($"🟢 SERVICE: Button saved to preferences, triggering event...");
        
        QuickButtonsChanged?.Invoke(this, _quickButtons.ToList());
        
        System.Diagnostics.Debug.WriteLine($"🟢 SERVICE: Event triggered for {_quickButtons.Count} buttons");
    }

    public async Task RemoveQuickButtonAsync(int buttonId)
    {
        var button = _quickButtons.FirstOrDefault(b => b.Id == buttonId);
        if (button != null)
        {
            _quickButtons.Remove(button);
            await SaveQuickButtonsAsync();
            
            QuickButtonsChanged?.Invoke(this, _quickButtons.ToList());
        }
    }

    public async Task UseButtonAsync(int buttonId)
    {
        var button = _quickButtons.FirstOrDefault(b => b.Id == buttonId);
        if (button != null)
        {
            button.LastUsed = DateTime.Now;
            button.IsEnabled = false;
            
            await SaveQuickButtonsAsync();
            QuickButtonsChanged?.Invoke(this, _quickButtons.ToList());
            
            // Re-enable after 15 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(15));
                button.IsEnabled = true;
                await SaveQuickButtonsAsync();
                QuickButtonsChanged?.Invoke(this, _quickButtons.ToList());
            });
        }
    }

    public bool IsButtonOnCooldown(int buttonId)
    {
        var button = _quickButtons.FirstOrDefault(b => b.Id == buttonId);
        
        return button != null && !button.IsEnabled;
    }

    private Task LoadQuickButtonsAsync()
    {
        try
        {
            var json = Preferences.Get(QUICK_BUTTONS_KEY, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                var buttons = JsonSerializer.Deserialize<List<QuickActivityButton>>(json);
                if (buttons != null)
                {
                    _quickButtons.Clear();
                    _quickButtons.AddRange(buttons);
                    
                    // Reset all buttons to enabled state (in case app was closed during cooldown)
                    foreach (var button in _quickButtons)
                    {
                        button.IsEnabled = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading quick buttons: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    private Task SaveQuickButtonsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_quickButtons);
            Preferences.Set(QUICK_BUTTONS_KEY, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving quick buttons: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
}
