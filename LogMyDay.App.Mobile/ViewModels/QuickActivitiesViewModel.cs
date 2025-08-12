using LogMyDay.App.Mobile.Models;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.DTOs;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace LogMyDay.App.Mobile.ViewModels;

public class QuickActivitiesViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private readonly QuickActivityService _quickActivityService;
    private ObservableCollection<QuickActivityButton> _quickButtons = new();
    private string _statusMessage = string.Empty;
    private bool _isError = false;
    private bool _hasStatusMessage = false;

    public ObservableCollection<QuickActivityButton> QuickButtons
    {
        get => _quickButtons;
        set
        {
            _quickButtons = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
            HasStatusMessage = !string.IsNullOrEmpty(value);
        }
    }

    public bool IsError
    {
        get => _isError;
        set
        {
            _isError = value;
            OnPropertyChanged();
        }
    }

    public bool HasStatusMessage
    {
        get => _hasStatusMessage;
        set
        {
            _hasStatusMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand UseButtonCommand { get; }
    public ICommand DeleteButtonCommand { get; }

    public QuickActivitiesViewModel(ApiService apiService, QuickActivityService quickActivityService)
    {
        _apiService = apiService;
        _quickActivityService = quickActivityService;
        
        UseButtonCommand = new Command<QuickActivityButton>(async (button) => await UseButton(button));
        DeleteButtonCommand = new Command<QuickActivityButton>(async (button) => await DeleteButton(button));
        
        _quickActivityService.QuickButtonsChanged += OnQuickButtonsChanged;
        
        _ = LoadQuickButtonsAsync();
    }

    private async Task LoadQuickButtonsAsync()
    {
        try
        {
            var buttons = await _quickActivityService.GetQuickButtonsAsync();
            QuickButtons = new ObservableCollection<QuickActivityButton>(buttons);
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Error loading quick buttons: {ex.Message}");
        }
    }

    public async Task RefreshButtonsAsync()
    {
        await LoadQuickButtonsAsync();
    }

    private async Task UseButton(QuickActivityButton button)
    {
        if (!button.IsEnabled)
        {
            ShowErrorMessage("Button is on cooldown, please wait...");
            
            return;
        }

        try
        {
            // Create activity request
            var request = new ActivityRequest
            {
                PrimaryTagId = button.TagId,
                Description = button.Value,
                DateStarted = DateTime.Now,
                DateFinished = null // Will be set based on tag configuration
            };

            var success = await _apiService.CreateActivityAsync(request);
            
            if (success)
            {
                await _quickActivityService.UseButtonAsync(button.Id);
                ShowSuccessMessage($"Activity '{button.Name}' created successfully! Button disabled for 15 seconds.");
            }
            else
            {
                ShowErrorMessage("Failed to create activity. Please try again.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Error creating activity: {ex.Message}");
        }
    }

    private async Task DeleteButton(QuickActivityButton button)
    {
        try
        {
            bool confirmed = false;
            var app = Application.Current;
            var page = app?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                confirmed = await page.DisplayAlert(
                    "Delete Quick Activity",
                    $"Are you sure you want to delete '{button.Name}'?",
                    "Delete",
                    "Cancel");
            }

            if (confirmed)
            {
                await _quickActivityService.RemoveQuickButtonAsync(button.Id);
                ShowSuccessMessage($"Quick activity '{button.Name}' deleted.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Error deleting button: {ex.Message}");
        }
    }

    private void OnQuickButtonsChanged(object? sender, List<QuickActivityButton> updatedButtons)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            QuickButtons.Clear();
            foreach (var button in updatedButtons)
            {
                QuickButtons.Add(button);
            }
        });
    }

    private void ShowSuccessMessage(string message)
    {
        IsError = false;
        StatusMessage = message;
        
        // Clear message after 3 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            MainThread.BeginInvokeOnMainThread(() => StatusMessage = string.Empty);
        });
    }

    private void ShowErrorMessage(string message)
    {
        IsError = true;
        StatusMessage = message;
        
        // Clear message after 5 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);
            MainThread.BeginInvokeOnMainThread(() => StatusMessage = string.Empty);
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
