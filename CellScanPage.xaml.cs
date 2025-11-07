using System;
using System.Linq;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui;

namespace ScanPackage;

public partial class CellScanPage : ContentPage
{
    private readonly System.Threading.Tasks.TaskCompletionSource<string?> _tcs;
    private bool _completed;

    public CellScanPage(System.Threading.Tasks.TaskCompletionSource<string?> tcs)
    {
        InitializeComponent();
        _tcs = tcs;

        // Cải thiện nhận dạng: hỗ trợ quét ở mọi góc độ
        // AutoRotate, TryHarder, TryInverted giúp quét được khi điện thoại nghiêng
        BarcodeView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.Code128 |
                      BarcodeFormat.Code39  |
                      BarcodeFormat.Code93  |
                      BarcodeFormat.Ean13   |
                      BarcodeFormat.Ean8    |
                      BarcodeFormat.Codabar |
                      BarcodeFormat.UpcA    |
                      BarcodeFormat.UpcE    |
                      BarcodeFormat.QrCode  |
                      BarcodeFormat.DataMatrix |
                      BarcodeFormat.Pdf417,
            AutoRotate = true,      // Tự động xoay để nhận diện ở mọi góc
            TryHarder = true,       // Cố gắng quét kỹ hơn (chậm hơn nhưng chính xác hơn)
            TryInverted = true,     // Thử quét cả ảnh đảo ngược
            Multiple = false        // Chỉ lấy 1 kết quả đầu tiên
        };
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (!_completed)
        {
            _completed = true;
            _tcs.TrySetResult(null);
        }
        await Navigation.PopModalAsync();
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_completed) return;

        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;

        _completed = true;
        _tcs.TrySetResult(value.Trim());
        await MainThread.InvokeOnMainThreadAsync(async () => await Navigation.PopModalAsync());
    }
}



