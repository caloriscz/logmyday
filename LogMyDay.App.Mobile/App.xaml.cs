namespace LogMyDay.App.Mobile;

public partial class App : Application
{
    public App()
    {
        System.Diagnostics.Debug.WriteLine("App: Constructor started");
        InitializeComponent();
        System.Diagnostics.Debug.WriteLine("App: Constructor completed");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        System.Diagnostics.Debug.WriteLine("App: CreateWindow called");
        try
        {
            System.Diagnostics.Debug.WriteLine("App: Creating MainPage instance");
            var mainPage = new MainPage();
            System.Diagnostics.Debug.WriteLine("App: MainPage created successfully");
            
            var window = new Window(mainPage)
            {
                Title = "LogMyDay Mobile"
            };
            System.Diagnostics.Debug.WriteLine("App: Window created successfully");
            return window;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App: CreateWindow error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"App: CreateWindow stack trace: {ex.StackTrace}");
            throw;
        }
    }
}
