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
}
