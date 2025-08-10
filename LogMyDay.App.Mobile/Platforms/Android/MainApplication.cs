using Android.App;
using Android.Runtime;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Mobile.Platforms.Android;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp()
    {
        try
        {
            return MauiProgram.CreateMauiApp();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating MAUI app: {ex}");
            throw;
        }
    }

    public override void OnCreate()
    {
        try
        {
            base.OnCreate();
            System.Diagnostics.Debug.WriteLine("MainApplication created successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MainApplication OnCreate: {ex}");
            throw;
        }
    }
}
