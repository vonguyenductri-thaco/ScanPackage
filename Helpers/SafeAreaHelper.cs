using Microsoft.Maui.Controls;
using System;

#if ANDROID
using Microsoft.Maui.Platform;
using Android.Views;
#endif

namespace ScanPackage.Helpers;

/// <summary>
/// Helper class để xử lý Safe Area Insets cho các thiết bị có notch (tai thỏ) hoặc camera rùi
/// </summary>
public static class SafeAreaHelper
{
    /// <summary>
    /// Áp dụng Safe Area Insets cho Header và Footer của một ContentPage
    /// </summary>
    /// <param name="headerGrid">Grid header cần áp dụng padding top</param>
    /// <param name="footerGrid">Grid footer cần áp dụng padding bottom (optional)</param>
    /// <param name="headerMinHeight">Chiều cao tối thiểu của header (default: 44)</param>
    /// <param name="footerMinHeight">Chiều cao tối thiểu của footer (default: 90)</param>
    public static void ApplySafeAreaInsets(
        Grid? headerGrid, 
        Grid? footerGrid = null,
        double headerMinHeight = 44,
        double footerMinHeight = 90)
    {
        try
        {
#if ANDROID
            var safeInsets = GetAndroidSafeAreaInsets();

            // Áp dụng cho Header (notch/status bar ở trên)
            if (headerGrid != null && safeInsets.Top > 0)
            {
                headerGrid.Padding = new Thickness(10, safeInsets.Top, 10, 0);
                
                // Tính toán chiều cao: safe area + min height
                if (headerGrid.HeightRequest < 0 || headerGrid.HeightRequest == -1)
                {
                    headerGrid.HeightRequest = safeInsets.Top + headerMinHeight;
                }
                else
                {
                    headerGrid.MinimumHeightRequest = safeInsets.Top + headerMinHeight;
                }
            }

            // Áp dụng cho Footer (navigation bar ở dưới)
            if (footerGrid != null && safeInsets.Bottom > 0)
            {
                footerGrid.Padding = new Thickness(10, 20, 10, safeInsets.Bottom + 10);
                
                if (footerGrid.HeightRequest < 0 || footerGrid.HeightRequest == -1)
                {
                    footerGrid.HeightRequest = safeInsets.Bottom + footerMinHeight;
                }
                else
                {
                    footerGrid.MinimumHeightRequest = safeInsets.Bottom + footerMinHeight;
                }
            }
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] ApplySafeAreaInsets error: {ex.Message}");
        }
    }

    /// <summary>
    /// Lấy Safe Area Insets từ Android Window
    /// </summary>
    /// <returns>Thickness chứa các giá trị safe area insets</returns>
    public static Thickness GetSafeAreaInsets()
    {
#if ANDROID
        return GetAndroidSafeAreaInsets();
#else
        return new Thickness(0);
#endif
    }

#if ANDROID
    private static Thickness GetAndroidSafeAreaInsets()
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity?.Window?.DecorView?.RootWindowInsets == null)
            {
                System.Diagnostics.Debug.WriteLine("[SafeAreaHelper] Window or RootWindowInsets is null, using fallback");
                return GetFallbackSafeAreaInsets();
            }

            var insets = activity.Window.DecorView.RootWindowInsets;
            var density = activity.Resources?.DisplayMetrics?.Density ?? 1;

            System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] Device: {Android.OS.Build.Manufacturer} {Android.OS.Build.Model}, Android {Android.OS.Build.VERSION.Release} (API {(int)Android.OS.Build.VERSION.SdkInt})");

        
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var windowInsets = insets.GetInsetsIgnoringVisibility(WindowInsets.Type.SystemBars());
                var cutoutInsets = insets.GetInsetsIgnoringVisibility(WindowInsets.Type.DisplayCutout());

                
                var combinedTop = Math.Max(windowInsets.Top, cutoutInsets.Top);
                var combinedBottom = Math.Max(windowInsets.Bottom, cutoutInsets.Bottom);

                var result = new Thickness(
                    windowInsets.Left / density,
                    combinedTop / density,
                    windowInsets.Right / density,
                    combinedBottom / density
                );

                System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] API 30+ insets: {result.Left}, {result.Top}, {result.Right}, {result.Bottom}");
                return result;
            }
            else
            {
               
                var baseResult = new Thickness(
                    insets.SystemWindowInsetLeft / density,
                    insets.SystemWindowInsetTop / density,
                    insets.SystemWindowInsetRight / density,
                    insets.SystemWindowInsetBottom / density
                );

               
                var optimizedResult = OptimizeForSamsung(baseResult, activity);
                
                System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] API 28-29 insets: {optimizedResult.Left}, {optimizedResult.Top}, {optimizedResult.Right}, {optimizedResult.Bottom}");
                return optimizedResult;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] GetAndroidSafeAreaInsets error: {ex.Message}");
            return GetFallbackSafeAreaInsets();
        }
    }

    private static Thickness OptimizeForSamsung(Thickness baseInsets, Android.App.Activity activity)
    {
        try
        {
            var manufacturer = Android.OS.Build.Manufacturer?.ToLower() ?? "";
            var model = Android.OS.Build.Model?.ToLower() ?? "";

            if (!manufacturer.Contains("samsung"))
                return baseInsets;

            System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] Samsung device detected: {model}");

            // Samsung A30 specific optimizations
            if (model.Contains("a30") || model.Contains("sm-a305"))
            {
                // Samsung A30 có Infinity-U display (notch giọt nước)
                // Status bar height thường là 24dp, nhưng với notch có thể cao hơn
                var displayMetrics = activity.Resources?.DisplayMetrics;
                var density = displayMetrics?.Density ?? 1;

                // Nếu top inset quá nhỏ, có thể là do One UI không báo cáo đúng
                if (baseInsets.Top < 30) // 30dp là minimum cho Samsung A30
                {
                    var adjustedTop = Math.Max(baseInsets.Top, 44); // Force minimum 44dp
                    System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] Samsung A30: Adjusted top from {baseInsets.Top} to {adjustedTop}");
                    
                    return new Thickness(
                        baseInsets.Left,
                        adjustedTop,
                        baseInsets.Right,
                        Math.Max(baseInsets.Bottom, 24) // Ensure minimum bottom padding
                    );
                }
            }

            // General Samsung optimizations
            // Samsung devices sometimes report incorrect bottom insets
            if (baseInsets.Bottom == 0)
            {
                // Check if navigation bar is visible
                var hasNavigationBar = HasNavigationBar(activity);
                if (hasNavigationBar)
                {
                    var adjustedBottom = 48; // Standard navigation bar height
                    System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] Samsung: Added navigation bar padding {adjustedBottom}");
                    
                    return new Thickness(
                        baseInsets.Left,
                        baseInsets.Top,
                        baseInsets.Right,
                        adjustedBottom
                    );
                }
            }

            return baseInsets;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] OptimizeForSamsung error: {ex.Message}");
            return baseInsets;
        }
    }

    private static bool HasNavigationBar(Android.App.Activity activity)
    {
        try
        {
            var resources = activity.Resources;
            var resourceId = resources.GetIdentifier("config_showNavigationBar", "bool", "android");
            
            if (resourceId > 0)
            {
                return resources.GetBoolean(resourceId);
            }

            // Fallback: check if device has hardware keys
            var hasMenuKey = Android.Views.ViewConfiguration.Get(activity).HasPermanentMenuKey;
            var hasBackKey = Android.Views.KeyCharacterMap.DeviceHasKey(Android.Views.Keycode.Back);
            
            return !(hasMenuKey || hasBackKey);
        }
        catch
        {
            return true; // Assume has navigation bar if can't determine
        }
    }

    private static Thickness GetFallbackSafeAreaInsets()
    {
        try
        {
            var manufacturer = Android.OS.Build.Manufacturer?.ToLower() ?? "";
            var model = Android.OS.Build.Model?.ToLower() ?? "";

            System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] Using fallback for {manufacturer} {model}");

            // Samsung A30 fallback values
            if (manufacturer.Contains("samsung") && (model.Contains("a30") || model.Contains("sm-a305")))
            {
                return new Thickness(0, 44, 0, 48); // Top: notch + status bar, Bottom: navigation bar
            }

            // General Samsung fallback
            if (manufacturer.Contains("samsung"))
            {
                return new Thickness(0, 36, 0, 48);
            }

            // Generic fallback
            return new Thickness(0, 24, 0, 48);
        }
        catch
        {
            return new Thickness(0, 24, 0, 48);
        }
    }
#endif

    /// <summary>
    /// Kiểm tra xem thiết bị có notch/camera rùi không
    /// </summary>
    /// <returns>True nếu thiết bị có notch/camera rùi</returns>
    public static bool HasNotch()
    {
        var insets = GetSafeAreaInsets();
        return insets.Top > 24 || insets.Bottom > 0; // Status bar thông thường ~24dp
    }

    /// <summary>
    /// Extension method để áp dụng Safe Area cho ContentPage
    /// </summary>
    public static void ApplySafeArea(this ContentPage page, string headerGridName = "HeaderGrid", string footerGridName = "FooterGrid")
    {
        try
        {
            var headerGrid = page.FindByName<Grid>(headerGridName);
            var footerGrid = page.FindByName<Grid>(footerGridName);
            
            ApplySafeAreaInsets(headerGrid, footerGrid);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] ApplySafeArea extension error: {ex.Message}");
        }
    }
}
