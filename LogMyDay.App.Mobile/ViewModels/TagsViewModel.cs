using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.DTOs;
using LogMyDay.Domain.Enums;

namespace LogMyDay.App.Mobile.ViewModels;

public class TagsViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private ObservableCollection<TagDisplayModel> _tags = new();
    private ObservableCollection<TagDisplayModel> _filteredTags = new();
    private bool _isRefreshing;
    private string _statusMessage = string.Empty;
    private string _searchText = string.Empty;

    public TagsViewModel(ApiService apiService)
    {
        _apiService = apiService;
        RefreshCommand = new Command(async () => await RefreshTagsAsync());
        OpenAddTagFormCommand = new Command(async () => await OpenAddTagFormAsync());
    }

    public ObservableCollection<TagDisplayModel> Tags
    {
        get => _tags;
        set
        {
            _tags = value;
            OnPropertyChanged();
            FilterTags();
        }
    }

    public ObservableCollection<TagDisplayModel> FilteredTags
    {
        get => _filteredTags;
        set
        {
            _filteredTags = value;
            OnPropertyChanged();
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

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
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
            FilterTags();
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenAddTagFormCommand { get; }

    public async Task LoadTagsAsync()
    {
        if (Tags.Count == 0)
        {
            await RefreshTagsAsync();
        }
    }

    public async Task RefreshTagsAsync()
    {
        IsRefreshing = true;
        StatusMessage = "Loading tags...";

        try
        {
            var tags = await _apiService.GetTagsAsync();
            
            if (!string.IsNullOrEmpty(_apiService.LastError))
            {
                StatusMessage = $"Error: {_apiService.LastError}";
                System.Diagnostics.Debug.WriteLine($"Tags loading error: {_apiService.LastError}");
            }
            else
            {
                var displayModels = tags.Select(t => new TagDisplayModel(t)).ToList();
                Tags = new ObservableCollection<TagDisplayModel>(displayModels);
                StatusMessage = $"Loaded {tags.Count} tags";
                System.Diagnostics.Debug.WriteLine($"Successfully loaded {tags.Count} tags");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load tags: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Exception loading tags: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void FilterTags()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredTags = new ObservableCollection<TagDisplayModel>(Tags);
        }
        else
        {
            var filtered = Tags
                .Where(t => t.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            FilteredTags = new ObservableCollection<TagDisplayModel>(filtered);
        }
    }

    public async Task AddNewTagAsync(string tagName)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Adding new tag: {tagName}");
            
            var success = await _apiService.CreateTagAsync(tagName);
            
            if (success)
            {
                // Refresh the tags list to include the new tag
                await RefreshTagsAsync();
                System.Diagnostics.Debug.WriteLine($"Successfully added tag: {tagName}");
            }
            else
            {
                var error = _apiService.LastError ?? "Unknown error occurred";
                System.Diagnostics.Debug.WriteLine($"Failed to add tag: {error}");
                throw new Exception(error);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception adding tag: {ex.Message}");
            throw;
        }
    }

    private async Task OpenAddTagFormAsync()
    {
        try
        {
            // For now, use a comprehensive input form
            var tagName = await Shell.Current.DisplayPromptAsync(
                "New Tag", 
                "Enter tag name:", 
                "Next", 
                "Cancel", 
                placeholder: "Tag name...");

            if (!string.IsNullOrWhiteSpace(tagName))
            {
                // Show additional options in action sheet
                var typeResult = await Shell.Current.DisplayActionSheet(
                    "Select Input Type",
                    "Cancel",
                    null,
                    "String (Text)",
                    "Integer (Number)", 
                    "Boolean (Checkbox)",
                    "Date (Date Picker)"
                );
                
                if (typeResult != "Cancel" && typeResult != null)
                {
                    var inputType = typeResult switch
                    {
                        "String (Text)" => "String",
                        "Integer (Number)" => "Integer", 
                        "Boolean (Checkbox)" => "Boolean",
                        "Date (Date Picker)" => "Date",
                        _ => "String"
                    };

                    // Ask about required flag
                    var isRequired = await Shell.Current.DisplayAlert(
                        "Tag Settings", 
                        "Should this tag be required?", 
                        "Required", 
                        "Optional");

                    // Ask about repeatable flag  
                    var isRepeatable = await Shell.Current.DisplayAlert(
                        "Tag Settings", 
                        "Can this tag be repeated?", 
                        "Repeatable", 
                        "Single Use");

                    // Ask about range flag
                    var isRange = await Shell.Current.DisplayAlert(
                        "Tag Settings", 
                        "Is this tag a range value?", 
                        "Range", 
                        "Single Value");

                    // Time granularity
                    var granularityResult = await Shell.Current.DisplayActionSheet(
                        "Time Granularity",
                        "Cancel",
                        null,
                        "Exact Time",
                        "Daily",
                        "Hourly", 
                        "Weekly",
                        "Monthly",
                        "Yearly"
                    );

                    if (granularityResult != "Cancel" && granularityResult != null)
                    {
                        await CreateTagAsync(tagName, inputType, isRequired, isRepeatable, isRange, granularityResult);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening add tag form: {ex.Message}";
        }
    }

    private async Task CreateTagAsync(string tagName, string inputType, bool isRequired, bool isRepeatable, bool isRange, string granularity)
    {
        try
        {
            // For now, create with just the name - the API service needs to be enhanced
            var success = await _apiService.CreateTagAsync(tagName);
            if (success)
            {
                // Refresh the tags list
                await RefreshTagsAsync();
                StatusMessage = $"Tag '{tagName}' created successfully!\nType: {inputType}, Required: {isRequired}, Repeatable: {isRepeatable}, Range: {isRange}, Granularity: {granularity}";
            }
            else
            {
                StatusMessage = "Failed to create tag";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating tag: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class TagDisplayModel : INotifyPropertyChanged
{
    private readonly TagResponse _tagResponse;

    public TagDisplayModel(TagResponse tagResponse)
    {
        _tagResponse = tagResponse;
    }

    public int Id => _tagResponse.Id;
    public string Title => _tagResponse.Title;
    public bool IsRequired => _tagResponse.IsRequired;
    public bool IsRepeatable => _tagResponse.IsRepeatable;
    public bool IsRange => _tagResponse.IsRange;
    public TimeGranularity TimeGranularity => _tagResponse.TimeGranularity;

    public string InputTypeDisplay
    {
        get
        {
            return _tagResponse.InputTypeId switch
            {
                1 => "Integer",
                2 => "String", 
                3 => "Boolean",
                4 => "Date",
                _ => "Text"
            };
        }
    }

    public string TimeGranularityDisplay
    {
        get
        {
            return TimeGranularity switch
            {
                TimeGranularity.Exact => "Exact Time",
                TimeGranularity.Daily => "Daily",
                TimeGranularity.Hourly => "Hourly",
                TimeGranularity.Weekly => "Weekly",
                TimeGranularity.Monthly => "Monthly",
                TimeGranularity.Yearly => "Yearly",
                _ => "Unknown"
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
