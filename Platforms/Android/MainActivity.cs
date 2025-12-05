using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui;

namespace ScanPackage
{
    // Sử dụng theme không có splash screen để app mở nhanh hơn
    [Activity(Theme = "@style/Maui.MainTheme.NoActionBar", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            // Enable edge-to-edge display và display cutout support
            EnableEdgeToEdge();
        }

        private void EnableEdgeToEdge()
        {
            var manufacturer = Build.Manufacturer?.ToLower() ?? "";
            var model = Build.Model?.ToLower() ?? "";
            
            System.Diagnostics.Debug.WriteLine($"[MainActivity] Configuring for {manufacturer} {model}, Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt})");

            if (Build.VERSION.SdkInt >= BuildVersionCodes.P) // Android 9+ (API 28+)
            {
                // Cho phép app vẽ vào vùng cutout - quan trọng cho Samsung A30
                if (Window?.Attributes != null)
                {
                    Window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
                }
                System.Diagnostics.Debug.WriteLine("[MainActivity] Display cutout mode: ShortEdges");
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R) // Android 11+ (API 30+)
            {
                // Edge-to-edge cho Android 11+
                Window?.SetDecorFitsSystemWindows(false);
                System.Diagnostics.Debug.WriteLine("[MainActivity] SetDecorFitsSystemWindows(false)");
            }
            else if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop) // Android 5+ (API 21+)
            {
                // Samsung-specific optimizations
                if (manufacturer.Contains("samsung"))
                {
                    ConfigureForSamsung();
                }
                else
                {
                    // Standard edge-to-edge cho non-Samsung devices
                    Window?.SetFlags(WindowManagerFlags.LayoutNoLimits, WindowManagerFlags.LayoutNoLimits);
                }
                
                // Transparent status bar và navigation bar
                Window?.SetStatusBarColor(Android.Graphics.Color.Transparent);
                Window?.SetNavigationBarColor(Android.Graphics.Color.Transparent);
                
                System.Diagnostics.Debug.WriteLine("[MainActivity] Transparent system bars enabled");
            }

            System.Diagnostics.Debug.WriteLine($"[MainActivity] Edge-to-edge configuration completed");
        }

        private void ConfigureForSamsung()
        {
            try
            {
                var model = Build.Model?.ToLower() ?? "";
                System.Diagnostics.Debug.WriteLine($"[MainActivity] Samsung-specific configuration for {model}");

                // Samsung A30 có thể cần cấu hình đặc biệt
                if (model.Contains("a30") || model.Contains("sm-a305"))
                {
                    // Samsung A30 với One UI có thể cần approach khác
                    // Thay vì LayoutNoLimits, dùng system UI flags
                    var decorView = Window?.DecorView;
                    if (decorView != null)
                    {
                        var flags = (int)(
                            SystemUiFlags.LayoutStable |
                            SystemUiFlags.LayoutHideNavigation |
                            SystemUiFlags.LayoutFullscreen
                        );
                        
                        decorView.SystemUiVisibility = (StatusBarVisibility)flags;
                        System.Diagnostics.Debug.WriteLine("[MainActivity] Samsung A30: Applied SystemUiFlags");
                    }
                }
                else
                {
                    // General Samsung devices
                    Window?.SetFlags(WindowManagerFlags.LayoutNoLimits, WindowManagerFlags.LayoutNoLimits);
                    System.Diagnostics.Debug.WriteLine("[MainActivity] Samsung: Applied LayoutNoLimits");
                }

                // Ensure immersive mode for Samsung
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
                {
                    Window?.DecorView?.SetOnSystemUiVisibilityChangeListener(new SystemUiVisibilityChangeListener());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] ConfigureForSamsung error: {ex.Message}");
                // Fallback to standard configuration
                Window?.SetFlags(WindowManagerFlags.LayoutNoLimits, WindowManagerFlags.LayoutNoLimits);
            }
        }
    }

    // Helper class for Samsung system UI visibility changes
    public class SystemUiVisibilityChangeListener : Java.Lang.Object, Android.Views.View.IOnSystemUiVisibilityChangeListener
    {
        public void OnSystemUiVisibilityChange(StatusBarVisibility visibility)
        {
            System.Diagnostics.Debug.WriteLine($"[MainActivity] System UI visibility changed: {visibility}");
        }
    }
}
