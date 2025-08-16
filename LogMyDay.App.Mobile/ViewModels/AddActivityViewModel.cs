using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Mobile.ViewModels;

public class AddActivityViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private ObservableCollection<TagResponse> _tags = new();
    private TagResponse? _selectedTag;
    private DateTime _startDate = DateTime.Today;
    private TimeSpan _startTime = DateTime.Now.TimeOfDay;
    private DateTime _endDate = DateTime.Today;
    private TimeSpan _endTime = DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1));
    private string _activityValue = string.Empty;
    private bool _booleanValue = false;
    private DateTime _dateValue = DateTime.Today;
    private string _description = string.Empty;
    private bool _addAnother = false;
    private bool _isSaving = false;

    public AddActivityViewModel(ApiService apiService)
    {
        _apiService = apiService;
        SaveCommand = new Command(async () => await SaveActivityAsync(), () => !IsSaving);
    }

    public ObservableCollection<TagResponse> Tags
    {
        get => _tags;
        set
        {
            _tags = value;
            OnPropertyChanged();
        }
    }

    public TagResponse? SelectedTag
    {
        get => _selectedTag;
        set
        {
            _selectedTag = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowValueInput));
            OnPropertyChanged(nameof(ShowIntegerInput));
            OnPropertyChanged(nameof(ShowStringInput));
            OnPropertyChanged(nameof(ShowBooleanInput));
            OnPropertyChanged(nameof(ShowDateInput));
        }
    }

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            _startDate = value;
            OnPropertyChanged();
        }
    }

    public TimeSpan StartTime
    {
        get => _startTime;
        set
        {
            _startTime = value;
            OnPropertyChanged();
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            _endDate = value;
            OnPropertyChanged();
        }
    }

    public TimeSpan EndTime
    {
        get => _endTime;
        set
        {
            _endTime = value;
            OnPropertyChanged();
        }
    }

    public string ActivityValue
    {
        get => _activityValue;
        set
        {
            _activityValue = value;
            OnPropertyChanged();
        }
    }

    public bool BooleanValue
    {
        get => _booleanValue;
        set
        {
            _booleanValue = value;
            OnPropertyChanged();
        }
    }

    public DateTime DateValue
    {
        get => _dateValue;
        set
        {
            _dateValue = value;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            OnPropertyChanged();
        }
    }

    public bool AddAnother
    {
        get => _addAnother;
        set
        {
            _addAnother = value;
            OnPropertyChanged();
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            _isSaving = value;
            OnPropertyChanged();
            ((Command)SaveCommand).ChangeCanExecute();
        }
    }

    public bool ShowValueInput => SelectedTag?.InputTypeId != null && SelectedTag.InputTypeId > 0;

    public bool ShowIntegerInput => SelectedTag?.InputTypeId == 1;
    public bool ShowStringInput => SelectedTag?.InputTypeId == 2;
    public bool ShowBooleanInput => SelectedTag?.InputTypeId == 3;
    public bool ShowDateInput => SelectedTag?.InputTypeId == 4;

    public ICommand SaveCommand { get; }

    public async Task LoadTagsAsync()
    {
        try
        {
            var tags = await _apiService.GetTagsAsync();
            Tags = new ObservableCollection<TagResponse>(tags);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading tags: {ex.Message}");
        }
    }

    public async Task<bool> SaveActivityAsync()
    {
        if (SelectedTag == null)
        {
            return false;
        }

        IsSaving = true;

        try
        {
            var startDateTime = StartDate.Date.Add(StartTime);
            var endDateTime = EndDate.Date.Add(EndTime);

            // Get the activity value based on input type
            var value = GetActivityValueForInputType();

            var request = new ActivityRequest
            {
                PrimaryTagId = SelectedTag.Id,
                DateStarted = startDateTime,
                DateFinished = endDateTime,
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim()
            };

            var response = await _apiService.CreateActivityAsync(request);
            
            if (!response)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving activity: {_apiService.LastError}");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"Activity saved successfully");
            
            if (!AddAnother)
            {
                ResetForm();
            }
            else
            {
                // Keep form values but clear value fields
                ActivityValue = string.Empty;
                BooleanValue = false;
                DateValue = DateTime.Today;
                Description = string.Empty;
                
                // Update times to now
                StartTime = DateTime.Now.TimeOfDay;
                EndTime = DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1));
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception saving activity: {ex.Message}");
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private string? GetActivityValueForInputType()
    {
        if (SelectedTag?.InputTypeId == null)
            return null;

        return SelectedTag.InputTypeId switch
        {
            1 => ActivityValue, // Integer
            2 => ActivityValue, // String
            3 => BooleanValue.ToString(), // Boolean
            4 => DateValue.ToString("yyyy-MM-dd"), // Date
            _ => null
        };
    }

    public void ResetForm()
    {
        SelectedTag = null;
        StartDate = DateTime.Today;
        StartTime = DateTime.Now.TimeOfDay;
        EndDate = DateTime.Today;
        EndTime = DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1));
        ActivityValue = string.Empty;
        BooleanValue = false;
        DateValue = DateTime.Today;
        Description = string.Empty;
        AddAnother = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
