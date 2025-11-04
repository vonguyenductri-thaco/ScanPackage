using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ScanPackage;

public partial class BarcodeScanPage : ContentPage
{
    private readonly Action<string> _onResult; // callback khi có kết quả
    private bool _isProcessing = false; // cờ để tránh xử lý nhiều lần

    public BarcodeScanPage(Action<string> onResult)
    {
        InitializeComponent();
        _onResult = onResult;

        cameraView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.All,
            AutoRotate = true,
            Multiple = false
        };
    }

    private async void CameraView_BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        // Lấy kết quả trước (có thể từ background thread)
        var result = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrEmpty(result)) return;

        // Tất cả thao tác UI và logic phải thực hiện trên main thread
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Kiểm tra cờ trên main thread để tránh race condition
            if (_isProcessing) return;

            _isProcessing = true; // đánh dấu đang xử lý

            // Ngừng nhận events để tránh xử lý nhiều lần
            cameraView.BarcodesDetected -= CameraView_BarcodesDetected;

            try
            {
                // Quay lại trang trước NGAY LẬP TỨC - không đợi callback
                if (Navigation.NavigationStack.Count > 1)
                {
                    await Navigation.PopAsync();
                }

                // Sau khi đã quay lại, mới gọi callback để nhập vào ô
                _onResult?.Invoke(result);
            }
            catch
            {
                // Nếu có lỗi, vẫn cố quay lại và gọi callback
                try
                {
                    if (Navigation.NavigationStack.Count > 1)
                    {
                        await Navigation.PopAsync();
                    }
                    _onResult?.Invoke(result);
                }
                catch { }
            }
        });
    }
}
