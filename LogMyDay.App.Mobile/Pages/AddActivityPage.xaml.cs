using LogMyDay.App.Mobile.ViewModels;

namespace LogMyDay.App.Mobile.Pages;

public partial class AddActivityPage : ContentPage
{
    private readonly AddActivityViewModel _viewModel;

    public AddActivityPage(AddActivityViewModel viewModel)
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

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var success = await _viewModel.SaveActivityAsync();
        
        if (success)
        {
            if (_viewModel.AddAnother)
            {
                await DisplayAlert("Success", "Activity saved! Add another activity.", "OK");
            }
            else
            {
                await DisplayAlert("Success", "Activity saved successfully!", "OK");
                await Shell.Current.GoToAsync("//home");
            }
        }
        else
        {
            await DisplayAlert("Error", "Failed to save activity. Please try again.", "OK");
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        if (HasUnsavedChanges())
        {
            var result = await DisplayAlert("Cancel", "Are you sure you want to discard this activity?", "Discard", "Continue");
            if (!result)
                return;
        }

        await Shell.Current.GoToAsync("//home");
    }

    private bool HasUnsavedChanges()
    {
        return _viewModel.SelectedTag != null || 
               !string.IsNullOrWhiteSpace(_viewModel.ActivityValue) ||
               !string.IsNullOrWhiteSpace(_viewModel.Description);
    }
}
