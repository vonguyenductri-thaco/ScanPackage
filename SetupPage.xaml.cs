using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

#if ANDROID
using Android.Views;
using Microsoft.Maui.Platform;
#endif

namespace ScanPackage;

public partial class SetupPage : ContentPage
{
    public SetupPage()
    {
        InitializeComponent();

        // Ngăn không cho hiển thị back button mặc định
        NavigationPage.SetHasBackButton(this, false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Apply safe area với delay nhỏ để đảm bảo window đã load
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            ApplySafeAreaInsets();
        });
    }

    // ==================== SAFE AREA HANDLING ====================

    private void ApplySafeAreaInsets()
    {
        try
        {
#if ANDROID
            var safeInsets = GetAndroidSafeAreaInsets();

            // Áp dụng cho Header (notch/status bar ở trên)
            if (safeInsets.Top > 0)
            {
                HeaderGrid.Padding = new Thickness(10, safeInsets.Top, 10, 0);
                HeaderGrid.HeightRequest = safeInsets.Top + 44; // 22 (font) + 16 (margin) + 6 (buffer)
            }

            // Áp dụng cho Footer (navigation bar ở dưới)
            if (safeInsets.Bottom > 0)
            {
                FooterGrid.Padding = new Thickness(10, 20, 10, safeInsets.Bottom + 10);
                FooterGrid.HeightRequest = safeInsets.Bottom + 90;
            }
#endif
        }
        catch (Exception ex)
        {
            // Ignore safe area errors
        }
    }

#if ANDROID
    private Thickness GetAndroidSafeAreaInsets()
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity?.Window?.DecorView?.RootWindowInsets == null)
                return new Thickness(0);

            var insets = activity.Window.DecorView.RootWindowInsets;

            // Android 11+ (API 30+)
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var windowInsets = insets.GetInsetsIgnoringVisibility(
                    Android.Views.WindowInsets.Type.SystemBars());

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
                // Android 10 and below (fallback)
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
            return new Thickness(0);
        }
    }
#endif

    // ==================== EVENT HANDLERS ====================

    /// <summary>
    /// Xử lý sự kiện click nút Back
    /// </summary>
    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            // Ignore navigation errors
        }
    }

    /// <summary>
    /// Xử lý sự kiện click nút "Tạo bảng"
    /// Validation input và navigate sang DataEntryPage
    /// </summary>
    private async void OnCreateClicked(object sender, EventArgs e)
    {
        try
        {
            // Kiểm tra ô "Số hàng" trống
            if (string.IsNullOrWhiteSpace(RowsEntry.Text))
            {
                await DisplayAlert(
                    "Cảnh báo",
                    "Vui lòng nhập số hàng!",
                    "OK");
                return;
            }

            // Kiểm tra ô "Số cột" trống
            if (string.IsNullOrWhiteSpace(ColsEntry.Text))
            {
                await DisplayAlert(
                    "Cảnh báo",
                    "Vui lòng nhập số cột!",
                    "OK");
                return;
            }

            // Lấy giá trị rows (đã chắc chắn là số do Keyboard="Numeric")
            int rows = int.Parse(RowsEntry.Text);

            // Kiểm tra số hàng <= 0
            if (rows <= 0)
            {
                await DisplayAlert(
                    "Cảnh báo",
                    "Số hàng phải lớn hơn 0!",
                    "OK");
                return;
            }

            // Lấy giá trị cols (đã chắc chắn là số do Keyboard="Numeric")
            int cols = int.Parse(ColsEntry.Text);

            // Kiểm tra số cột <= 0
            if (cols <= 0)
            {
                await DisplayAlert(
                    "Cảnh báo",
                    "Số cột phải lớn hơn 0!",
                    "OK");
                return;
            }

            // Giới hạn kích thước bảng
            if (rows > 10)
            {
                await DisplayAlert(
                    "Cảnh báo",
                    "Số hàng quá lớn! Tối đa 10 hàng.",
                    "OK");
                return;
            }

            if (cols > 20)
            {
                await DisplayAlert(
                    "Cảnh báo",
                    "Số cột quá lớn! Tối đa 20 cột.",
                    "OK");
                return;
            }

            // Hộp thoại xác nhận với 2 nút
            bool confirm = await DisplayAlert(
                "Xác nhận tạo bảng",
                $"Tạo bảng với {rows} hàng và {cols} cột?",
                "Xác nhận",
                "Hủy bỏ");

            if (confirm)
            {
                // Chuyển đến trang nhập dữ liệu
                await Navigation.PushAsync(new DataEntryPage(rows, cols));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Lỗi",
                $"Không thể tạo bảng:\n{ex.Message}",
                "OK");
        }
    }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Validate input entries khi người dùng thay đổi
    /// </summary>
    private void ValidateNumericInput(object sender, TextChangedEventArgs e)
    {
        if (sender is Entry entry)
        {
            // Chỉ cho phép nhập số
            if (!string.IsNullOrEmpty(e.NewTextValue) && !int.TryParse(e.NewTextValue, out _))
            {
                entry.Text = e.OldTextValue;
            }
        }
    }
}