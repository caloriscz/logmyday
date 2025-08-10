using LogMyDay.App.Mobile.Models;
using LogMyDay.App.Mobile.Services;
using LogMyDay.App.Mobile.ViewModels;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Mobile.Pages;

public partial class QuickActivitiesPage : ContentPage
{
    private readonly QuickActivitiesViewModel _viewModel;
    private readonly ApiService _apiService;
    private readonly QuickActivityService _quickActivityService;

    public QuickActivitiesPage(QuickActivitiesViewModel viewModel, ApiService apiService, QuickActivityService quickActivityService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _apiService = apiService;
        _quickActivityService = quickActivityService;
        BindingContext = _viewModel;
    }

    private async void OnAddButtonClicked(object sender, EventArgs e)
    {
        try
        {
            // Show dialog to get button configuration
            await ShowAddButtonDialog();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error adding button: {ex.Message}", "OK");
        }
    }

    private async Task ShowAddButtonDialog()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("QuickActivitiesPage: Starting ShowAddButtonDialog...");
            
            // First, get available tags
            var tags = await _apiService.GetTagsAsync();
            
            System.Diagnostics.Debug.WriteLine($"QuickActivitiesPage: Received {tags.Count} tags from API");
            
            if (tags.Count == 0)
            {
                await DisplayAlert("No Tags", "No tags available. Please create tags in the web application first.\n\nNote: Make sure the API credentials are correct and the server is accessible.", "OK");
                
                return;
            }

            // Show tag selection
            var tagNames = tags.Select(t => t.Title).ToArray();
            var selectedTagName = await DisplayActionSheet("Select Tag", "Cancel", null, tagNames);
            
            if (selectedTagName == null || selectedTagName == "Cancel")
            {
                return;
            }

            var selectedTag = tags.First(t => t.Title == selectedTagName);

            // Get button name
            var buttonName = await DisplayPromptAsync("Button Name", "Enter a name for this quick activity button:", placeholder: $"{selectedTag.Title} Button");
            
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                return;
            }

            // Get value if needed (based on tag type)
            string? value = null;
            if (selectedTag.TypeId.HasValue)
            {
                value = await GetValueForTagType(selectedTag);
                if (value == null) // User cancelled
                {
                    return;
                }
            }

            // Create the quick button
            var quickButton = new QuickActivityButton
            {
                Name = buttonName,
                TagId = selectedTag.Id,
                TagName = selectedTag.Title,
                Value = value
            };

            await _quickActivityService.AddQuickButtonAsync(quickButton);
            
            await DisplayAlert("Success", $"Quick activity button '{buttonName}' created!", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"QuickActivitiesPage Error in ShowAddButtonDialog: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            
            await DisplayAlert("Error", $"Failed to create quick button: {ex.Message}", "OK");
        }
    }

    private async Task<string?> GetValueForTagType(TagResponse tag)
    {
        return tag.TypeId switch
        {
            1 => await DisplayPromptAsync("Enter Value", $"Enter numeric value for {tag.Title}:", keyboard: Keyboard.Numeric),
            2 => await DisplayPromptAsync("Enter Value", $"Enter text value for {tag.Title}:"),
            3 => await GetBooleanValue(tag.Title),
            4 => await GetDateValue(tag.Title),
            _ => await DisplayPromptAsync("Enter Value", $"Enter value for {tag.Title}:")
        };
    }

    private async Task<string?> GetBooleanValue(string tagTitle)
    {
        var result = await DisplayActionSheet($"Select value for {tagTitle}", "Cancel", null, "True", "False");
        
        return result switch
        {
            "True" => "true",
            "False" => "false",
            _ => null
        };
    }

    private async Task<string?> GetDateValue(string tagTitle)
    {
        // For simplicity, we'll use today's date. In a more advanced implementation,
        // you could show a date picker
        var useToday = await DisplayAlert($"Date for {tagTitle}", "Use today's date?", "Yes", "Cancel");
        
        return useToday ? DateTime.Today.ToString("yyyy-MM-dd") : null;
    }
}
