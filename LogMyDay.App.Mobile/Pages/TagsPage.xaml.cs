using LogMyDay.App.Mobile.ViewModels;

namespace LogMyDay.App.Mobile.Pages;

public partial class TagsPage : ContentPage
{
    private readonly TagsViewModel _viewModel;

    public TagsPage(TagsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadTagsAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await _viewModel.RefreshTagsAsync();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchText = e.NewTextValue ?? string.Empty;
    }

    private async void OnAddTagClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Add tag clicked - Entry text: '{NewTagEntry?.Text}'");
        
        var tagName = NewTagEntry?.Text?.Trim();
        System.Diagnostics.Debug.WriteLine($"Processed tag name: '{tagName}'");
        
        if (string.IsNullOrEmpty(tagName))
        {
            await DisplayAlert("Error", "Please enter a tag name", "OK");
            return;
        }

        try
        {
            await _viewModel.AddNewTagAsync(tagName);
            if (NewTagEntry != null)
                NewTagEntry.Text = string.Empty; // Clear the input
            await DisplayAlert("Success", $"Tag '{tagName}' added successfully", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add tag: {ex.Message}", "OK");
        }
    }
}
