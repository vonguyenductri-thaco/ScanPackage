using Microsoft.Maui.Controls;
using ScanPackage.Helpers;
using System;
using System.Threading.Tasks;

#if ANDROID
using Android.OS;
#endif

namespace ScanPackage;

public partial class TestSafeAreaPage : ContentPage
{
    public TestSafeAreaPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            LoadSafeAreaInfo();
            LoadDeviceInfo();
        });
    }

    private void LoadSafeAreaInfo()
    {
        try
        {
            // Sử dụng SafeAreaHelper
            var safeInsets = SafeAreaHelper.GetSafeAreaInsets();
            
            // Debug log
            System.Diagnostics.Debug.WriteLine($"[TestSafeArea] Safe Area - Top: {safeInsets.Top}, Bottom: {safeInsets.Bottom}, Left: {safeInsets.Left}, Right: {safeInsets.Right}");
            
            // Áp dụng Safe Area
            SafeAreaHelper.ApplySafeAreaInsets(HeaderGrid, FooterGrid, 60, 80);
            
            // Hiển thị thông tin chi tiết
            var manufacturer = "";
            var model = "";
            var androidVersion = "";
            var apiLevel = "";
            
#if ANDROID
            manufacturer = Android.OS.Build.Manufacturer ?? "";
            model = Android.OS.Build.Model ?? "";
            androidVersion = Android.OS.Build.VERSION.Release ?? "";
            apiLevel = ((int)Android.OS.Build.VERSION.SdkInt).ToString();
#endif

            SafeAreaInfoLabel.Text = $"📊 SAFE AREA INSETS:\n" +
                                   $"• Top (Status Bar/Notch): {safeInsets.Top:F1}dp\n" +
                                   $"• Bottom (Navigation Bar): {safeInsets.Bottom:F1}dp\n" +
                                   $"• Left: {safeInsets.Left:F1}dp\n" +
                                   $"• Right: {safeInsets.Right:F1}dp\n\n" +
                                   $"🔍 PHÂN TÍCH:\n" +
                                   $"• {(safeInsets.Top > 24 ? "✅ Có Notch/Camera rùi" : "❌ Không có Notch")}\n" +
                                   $"• {(safeInsets.Bottom > 0 ? "✅ Có Navigation Bar" : "❌ Không có Navigation Bar")}\n" +
                                   $"• {(SafeAreaHelper.HasNotch() ? "📱 Thiết bị hiện đại" : "📱 Thiết bị thông thường")}\n\n" +
                                   $"🏭 SAMSUNG A30 SPECIFIC:\n" +
                                   $"• Manufacturer: {manufacturer}\n" +
                                   $"• Model: {model}\n" +
                                   $"• Android: {androidVersion} (API {apiLevel})\n" +
                                   $"• Is Samsung A30: {(model.ToLower().Contains("a30") || model.ToLower().Contains("sm-a305") ? "✅ YES" : "❌ NO")}\n" +
                                   $"• Expected Top: 44dp (Infinity-U notch)\n" +
                                   $"• Expected Bottom: 48dp (Navigation bar)\n" +
                                   $"• Status: {GetCompatibilityStatus(safeInsets, model)}";

            // Kiểm tra xem Safe Area có hoạt động không
            if (safeInsets.Top == 0 && safeInsets.Bottom == 0)
            {
                SafeAreaInfoLabel.Text += "\n\n⚠️ CẢNH BÁO: Safe Area = 0!\n" +
                                        "• Có thể MainActivity chưa enable edge-to-edge\n" +
                                        "• Hoặc Window chưa sẵn sàng\n" +
                                        "• Kiểm tra Output window để xem log";
                SafeAreaInfoLabel.BackgroundColor = Colors.LightPink;
            }
            else
            {
                SafeAreaInfoLabel.BackgroundColor = Colors.LightGreen;
            }
        }
        catch (Exception ex)
        {
            SafeAreaInfoLabel.Text = $"❌ Lỗi khi lấy Safe Area: {ex.Message}";
            SafeAreaInfoLabel.BackgroundColor = Colors.LightPink;
            System.Diagnostics.Debug.WriteLine($"[TestSafeArea] Error: {ex.Message}");
        }
    }

    private void LoadDeviceInfo()
    {
        try
        {
#if ANDROID
            var manufacturer = Build.Manufacturer ?? "Unknown";
            var model = Build.Model ?? "Unknown";
            var androidVersion = Build.VERSION.Release ?? "Unknown";
            var apiLevel = (int)Build.VERSION.SdkInt;
            var displayMetrics = Platform.CurrentActivity?.Resources?.DisplayMetrics;

            DeviceInfoLabel.Text = $"📱 THÔNG TIN THIẾT BỊ:\n" +
                                 $"• Manufacturer: {manufacturer}\n" +
                                 $"• Model: {model}\n" +
                                 $"• Android Version: {androidVersion}\n" +
                                 $"• API Level: {apiLevel}\n" +
                                 $"• Density: {displayMetrics?.Density:F2}\n" +
                                 $"• Screen Size (px): {displayMetrics?.WidthPixels}x{displayMetrics?.HeightPixels}\n" +
                                 $"• Screen Size (dp): {displayMetrics?.WidthPixels / displayMetrics?.Density:F0}x{displayMetrics?.HeightPixels / displayMetrics?.Density:F0}dp";
#else
            DeviceInfoLabel.Text = "📱 THÔNG TIN THIẾT BỊ:\n• Platform: Non-Android\n• Safe Area chỉ hoạt động trên Android";
#endif
        }
        catch (Exception ex)
        {
            DeviceInfoLabel.Text = $"❌ Không thể lấy thông tin thiết bị: {ex.Message}";
        }
    }

    private string GetCompatibilityStatus(Thickness safeInsets, string model)
    {
        try
        {
            var modelLower = model.ToLower();
            
            // Samsung A30 specific checks
            if (modelLower.Contains("a30") || modelLower.Contains("sm-a305"))
            {
                var topOk = safeInsets.Top >= 40; // Should be around 44dp
                var bottomOk = safeInsets.Bottom >= 20; // Should be around 48dp
                
                if (topOk && bottomOk)
                    return "✅ HOÀN HẢO - Safe Area hoạt động tốt";
                else if (topOk && !bottomOk)
                    return "⚠️ THIẾU BOTTOM - Navigation bar chưa được xử lý";
                else if (!topOk && bottomOk)
                    return "⚠️ THIẾU TOP - Notch chưa được xử lý";
                else
                    return "❌ CHƯA HOẠT ĐỘNG - Cần kiểm tra MainActivity";
            }
            
            // General Samsung devices
            if (model.ToLower().Contains("samsung"))
            {
                if (safeInsets.Top > 0 && safeInsets.Bottom > 0)
                    return "✅ TỐT - Samsung device được hỗ trợ";
                else
                    return "⚠️ CẦN KIỂM TRA - Một số insets = 0";
            }
            
            // Non-Samsung devices
            if (safeInsets.Top > 0 || safeInsets.Bottom > 0)
                return "✅ HOẠT ĐỘNG - Safe Area được phát hiện";
            else
                return "❌ KHÔNG HOẠT ĐỘNG - Safe Area = 0";
        }
        catch
        {
            return "❓ KHÔNG XÁC ĐỊNH - Lỗi khi kiểm tra";
        }
    }

    private async void OnTestCameraClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new TestCameraPage());
    }
}