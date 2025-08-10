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

    private async void OnTestApiClicked(object sender, EventArgs e)
    {
        try
        {
            await DisplayAlert("Testing API", "Starting API connection test...", "OK");
            
            // Test the API connection
            var isConnected = await _apiService.TestApiConnectionAsync();
            
            if (isConnected)
            {
                var tags = await _apiService.GetTagsAsync();
                var successMessage = $"✅ API CONNECTION SUCCESS!\n\n";
                successMessage += $"🏷️ Found {tags.Count} tags:\n";
                
                if (tags.Count > 0)
                {
                    foreach (var tag in tags.Take(5)) // Show first 5 tags
                    {
                        successMessage += $"• {tag.Title}\n";
                    }
                    if (tags.Count > 5)
                    {
                        successMessage += $"... and {tags.Count - 5} more";
                    }
                }
                
                await DisplayAlert("API Test Success", successMessage, "OK");
            }
            else
            {
                var errorMessage = "❌ API CONNECTION FAILED!\n\n";
                if (!string.IsNullOrEmpty(_apiService.LastError))
                {
                    errorMessage += "🔍 ERROR DETAILS:\n";
                    errorMessage += _apiService.LastError;
                }
                else
                {
                    errorMessage += "No specific error details available.";
                }
                
                await DisplayAlert("API Test Failed", errorMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            var exceptionDetails = $"🔥 EXCEPTION DURING API TEST:\n\n";
            exceptionDetails += $"💥 ERROR: {ex.Message}\n";
            exceptionDetails += $"📝 TYPE: {ex.GetType().Name}\n";
            if (ex.InnerException != null)
            {
                exceptionDetails += $"🔗 INNER: {ex.InnerException.Message}\n";
            }
            exceptionDetails += $"\n📍 STACK TRACE:\n{ex.StackTrace}";
            
            await DisplayAlert("Test Exception", exceptionDetails, "OK");
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
                // Show the actual error details from the API service
                var errorMessage = "❌ API CALL FAILED\n\n";
                
                if (!string.IsNullOrEmpty(_apiService.LastError))
                {
                    errorMessage += "🔍 EXCEPTION DETAILS:\n";
                    errorMessage += _apiService.LastError;
                }
                else
                {
                    errorMessage += "🔍 DEBUGGING INFO:\n";
                    errorMessage += "• API URL: https://logmyday.tadata.cz/api/tags\n";
                    errorMessage += "• Credentials: admin/secret123\n";
                    errorMessage += "• No exception details available\n";
                    errorMessage += "• API returned empty response";
                }
                
                errorMessage += "\n\n📝 NEXT STEPS:\n";
                errorMessage += "• Check internet connection\n";
                errorMessage += "• Verify server is running\n";
                errorMessage += "• Test API in browser/Postman";
                    
                await DisplayAlert("API Connection Failed", errorMessage, "OK");
                
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
            
            var detailedError = "🔥 EXCEPTION IN SHOWADDBUTTONDIALOG\n\n";
            detailedError += $"💥 ERROR: {ex.Message}\n";
            detailedError += $"📝 TYPE: {ex.GetType().Name}\n";
            if (ex.InnerException != null)
            {
                detailedError += $"🔗 INNER: {ex.InnerException.Message}\n";
            }
            detailedError += $"\n📍 STACK TRACE:\n{ex.StackTrace}";
            
            await DisplayAlert("Exception Details", detailedError, "OK");
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
