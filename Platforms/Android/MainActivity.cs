using Android.App;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui;

namespace ScanPackage
{
    // Sử dụng theme không có splash screen để app mở nhanh hơn
    [Activity(Theme = "@style/Maui.MainTheme.NoActionBar", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
