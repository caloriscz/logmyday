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
        base.OnCreate(savedInstanceState);
    }
}
