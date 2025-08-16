using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Mobile.ViewModels;

public class ActivitiesViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private ObservableCollection<ActivityDisplayModel> _activities = new();
    private ObservableCollection<ActivityDisplayModel> _allActivities = new();
    private DateTime _selectedDate = DateTime.Today;
    private bool _isRefreshing = false;
    private string _searchText = string.Empty;

    public ActivitiesViewModel(ApiService apiService)
    {
        _apiService = apiService;
        
        RefreshCommand = new Command(async () => await RefreshActivitiesAsync());
        PreviousDayCommand = new Command(() => SelectedDate = SelectedDate.AddDays(-1));
        NextDayCommand = new Command(() => SelectedDate = SelectedDate.AddDays(1));
        GoToTodayCommand = new Command(() => SelectedDate = DateTime.Today);
        AddActivityCommand = new Command(async () => await Shell.Current.GoToAsync("//addactivity"));
        ActivityTappedCommand = new Command<ActivityDisplayModel>(OnActivityTapped);
    }

    public ObservableCollection<ActivityDisplayModel> Activities
    {
        get => _activities;
        set
        {
            _activities = value;
            OnPropertyChanged();
        }
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            _selectedDate = value;
            OnPropertyChanged();
            _ = Task.Run(async () => await RefreshActivitiesAsync());
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            FilterActivities();
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand PreviousDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand GoToTodayCommand { get; }
    public ICommand AddActivityCommand { get; }
    public ICommand ActivityTappedCommand { get; }

    public async Task LoadActivitiesAsync()
    {
        await RefreshActivitiesAsync();
    }

    public async Task RefreshActivitiesAsync()
    {
        IsRefreshing = true;

        try
        {
            var startDate = SelectedDate.Date;
            var endDate = SelectedDate.Date.AddDays(1).AddTicks(-1);

            var activities = await _apiService.GetActivitiesAsync(
                startDate: startDate,
                endDate: endDate,
                pageSize: 100);

            if (!string.IsNullOrEmpty(_apiService.LastError))
            {
                System.Diagnostics.Debug.WriteLine($"Activities loading error: {_apiService.LastError}");
                _allActivities = new ObservableCollection<ActivityDisplayModel>();
                Activities = new ObservableCollection<ActivityDisplayModel>();
            }
            else
            {
                var displayModels = activities.Select(a => new ActivityDisplayModel(a)).ToList();
                _allActivities = new ObservableCollection<ActivityDisplayModel>(displayModels);
                Activities = new ObservableCollection<ActivityDisplayModel>(displayModels);
                System.Diagnostics.Debug.WriteLine($"Successfully loaded {activities.Count} activities for {SelectedDate:yyyy-MM-dd}");
                FilterActivities(); // Apply any existing search filter
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception loading activities: {ex.Message}");
            _allActivities = new ObservableCollection<ActivityDisplayModel>();
            Activities = new ObservableCollection<ActivityDisplayModel>();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async void OnActivityTapped(ActivityDisplayModel activity)
    {
        if (activity == null) return;

        // Show activity details in a popup or navigate to edit page
        var action = await Application.Current?.MainPage?.DisplayActionSheet(
            $"Activity: {activity.TagTitle}",
            "Cancel",
            null,
            "View Details",
            "Edit Activity",
            "Delete Activity");

        switch (action)
        {
            case "View Details":
                await ShowActivityDetails(activity);
                break;
            case "Edit Activity":
                // TODO: Navigate to edit page
                await Application.Current?.MainPage?.DisplayAlert("Edit", "Edit feature coming soon!", "OK");
                break;
            case "Delete Activity":
                await DeleteActivity(activity);
                break;
        }
    }

    private async Task ShowActivityDetails(ActivityDisplayModel activity)
    {
        var details = $"Tag: {activity.TagTitle}\n" +
                     $"Started: {activity.DateStarted:yyyy-MM-dd HH:mm}\n" +
                     $"Finished: {(activity.DateFinished?.ToString("yyyy-MM-dd HH:mm") ?? "Not finished")}\n";

        if (activity.HasValue)
        {
            details += $"Value: {activity.Value}\n";
        }

        if (activity.HasDescription)
        {
            details += $"Description: {activity.Description}";
        }

        await Application.Current?.MainPage?.DisplayAlert("Activity Details", details, "OK");
    }

    private async Task DeleteActivity(ActivityDisplayModel activity)
    {
        var confirmed = await Application.Current?.MainPage?.DisplayAlert(
            "Delete Activity",
            $"Are you sure you want to delete '{activity.TagTitle}'?",
            "Delete",
            "Cancel");

        if (confirmed == true)
        {
            var success = await _apiService.DeleteActivityAsync(activity.Id);
            
            if (success)
            {
                await RefreshActivitiesAsync();
                await Application.Current?.MainPage?.DisplayAlert("Success", "Activity deleted successfully.", "OK");
            }
            else
            {
                await Application.Current?.MainPage?.DisplayAlert("Error", "Failed to delete activity. Please try again.", "OK");
            }
        }
    }

    private void FilterActivities()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Activities = new ObservableCollection<ActivityDisplayModel>(_allActivities);
        }
        else
        {
            var filtered = _allActivities
                .Where(a => 
                    a.TagTitle.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (a.Description != null && a.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (a.Value != null && a.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            Activities = new ObservableCollection<ActivityDisplayModel>(filtered);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ActivityDisplayModel : INotifyPropertyChanged
{
    private readonly ActivityResponse _activityResponse;

    public ActivityDisplayModel(ActivityResponse activityResponse)
    {
        _activityResponse = activityResponse;
    }

    public int Id => _activityResponse.Id;
    public string TagTitle => _activityResponse.PrimaryTagName;
    public DateTime DateStarted => _activityResponse.DateStarted;
    public DateTime? DateFinished => _activityResponse.DateFinished;
    public string? Value => _activityResponse.PrimaryTagValue;
    public string? Description => _activityResponse.Description;

    public bool HasValue => !string.IsNullOrWhiteSpace(Value);
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public string TimeDisplay
    {
        get
        {
            if (DateFinished.HasValue)
            {
                return $"{DateStarted:HH:mm} - {DateFinished.Value:HH:mm}";
            }
            else
            {
                return $"{DateStarted:HH:mm}";
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
