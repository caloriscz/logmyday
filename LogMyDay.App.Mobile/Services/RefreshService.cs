namespace LogMyDay.App.Mobile.Services;

public static class RefreshService
{
    public static event EventHandler? RefreshRequested;

    public static void RequestRefresh()
    {
        RefreshRequested?.Invoke(null, EventArgs.Empty);
    }
}
