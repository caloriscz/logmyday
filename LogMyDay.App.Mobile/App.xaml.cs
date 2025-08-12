using LogMyDay.App.Mobile.Pages;

namespace LogMyDay.App.Mobile;

public partial class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
        
        // Register routes
        Routing.RegisterRoute("settings", typeof(SettingsPage));
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_mainPage)
        {
            Title = "LogMyDay Mobile"
        };
    }
}
