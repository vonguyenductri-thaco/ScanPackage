using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ScanPackage;

public partial class BarcodeScanPage : ContentPage
{
    private readonly Action<string> _onResult; // callback khi có kết quả

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
        var result = e.Results?.FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(result))
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _onResult?.Invoke(result); // trả kết quả về ô nhập
                await Navigation.PopAsync(); // quay lại trang bảng
            });
        }
    }
}
