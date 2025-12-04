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
                return new Thickness(0);

            var insets = activity.Window.DecorView.RootWindowInsets;

            // Android 11+ (API 30+) - Phương pháp mới
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var windowInsets = insets.GetInsetsIgnoringVisibility(
                    WindowInsets.Type.SystemBars());

                var density = activity.Resources?.DisplayMetrics?.Density ?? 1;

                return new Thickness(
                    windowInsets.Left / density,
                    windowInsets.Top / density,
                    windowInsets.Right / density,
                    windowInsets.Bottom / density
                );
            }
            else
            {
                // Android 10 và thấp hơn - Phương pháp cũ
                var density = activity.Resources?.DisplayMetrics?.Density ?? 1;

                return new Thickness(
                    insets.SystemWindowInsetLeft / density,
                    insets.SystemWindowInsetTop / density,
                    insets.SystemWindowInsetRight / density,
                    insets.SystemWindowInsetBottom / density
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SafeAreaHelper] GetAndroidSafeAreaInsets error: {ex.Message}");
            return new Thickness(0);
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
