using LogMyDay.App.Mobile.ViewModels;

namespace LogMyDay.App.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Focus the username entry when the page appears
        usernameEntry.Focus();
    }
}
