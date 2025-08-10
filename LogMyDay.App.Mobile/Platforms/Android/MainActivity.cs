using Android.App;
using Android.Content.PM;

namespace LogMyDay.App.Mobile.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, 
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | 
                          ConfigChanges.UiMode | ConfigChanges.ScreenLayout | 
                          ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        try
        {
            base.OnCreate(savedInstanceState);
            System.Diagnostics.Debug.WriteLine("MainActivity created successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MainActivity OnCreate: {ex}");
            throw;
        }
    }

    protected override void OnResume()
    {
        try
        {
            base.OnResume();
            System.Diagnostics.Debug.WriteLine("MainActivity resumed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MainActivity OnResume: {ex}");
        }
    }
}
